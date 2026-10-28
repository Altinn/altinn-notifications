using System.Collections.Immutable;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

using LoggedEntry = (string Channel, string Destination, string Status);

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class NotificationLogRepositoryTests : IAsyncLifetime
{
    private static readonly UnitOfWorkRepository _unitOfWorkRepository = new(ServiceUtil.GetSharedDataSource());

    private readonly List<Guid> _orderIdsToCleanup = [];
    private readonly List<Guid> _orderChainIdsToCleanup = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (Guid orderId in _orderIdsToCleanup)
        {
            await PostgreUtil.DeleteOrderFromDb(orderId);
            await PostgreUtil.DeleteNotificationLogFromDb(orderId);
        }

        await PostgreUtil.DeleteOrdersChainByOrderIds(_orderChainIdsToCleanup);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_WithChainedEmailOrder_PersistsAllNotificationLogProperties()
    {
        // Arrange
        var dialogId = Guid.NewGuid();
        const string transmissionId = "test-transmission-abc123";
        const string toAddress = "log-test@example.com";
        const string operationId = "op-id-test-abc123";
        const string resourceId = "ttd-resource";
        const string creatorName = "ttd";

        (Guid orderId, Guid orderChainId) = await PostgreUtil.PopulateDBWithChainedOrderAndEmailNotification(
            dialogId: dialogId,
            transmissionId: transmissionId,
            toAddress: toAddress,
            operationId: operationId,
            creatorName: creatorName,
            resourceId: resourceId);

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await InsertNotificationLogAsync(orderId);

        // Assert — nothing was skipped
        Assert.Empty(skippedNotificationIds);

        // operationId is set via updateemailnotification_v4
        await AssertPersistedEntryAsync(orderId, orderChainId, creatorName, dialogId, transmissionId, resourceId, "Email", toAddress, operationId);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_WithChainedSmsOrder_PersistsAllNotificationLogProperties()
    {
        // Arrange
        var dialogId = Guid.NewGuid();
        var gatewayReference = Guid.NewGuid().ToString();
        const string transmissionId = "test-transmission-sms-456";
        const string mobileNumber = "+4799999999";
        const string resourceId = "ttd-resource";
        const string creatorName = "ttd";

        (Guid orderId, Guid orderChainId) = await PostgreUtil.PopulateDBWithChainedOrderAndSmsNotification(
            dialogId: dialogId,
            transmissionId: transmissionId,
            mobileNumber: mobileNumber,
            gatewayReference: gatewayReference,
            creatorName: creatorName,
            resourceId: resourceId);

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await InsertNotificationLogAsync(orderId);

        // Assert — nothing was skipped
        Assert.Empty(skippedNotificationIds);

        await AssertPersistedEntryAsync(orderId, orderChainId, creatorName, dialogId, transmissionId, resourceId, "Sms", mobileNumber, gatewayReference);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_WithStandaloneOrder_ChainDerivedFieldsAreNull()
    {
        // Arrange — standalone order has no orderschain row, so orderchainid is NULL.
        (NotificationOrder order, EmailNotification _) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateCronJob: true, simulateConsumers: true, resultType: EmailNotificationResultType.Delivered);

        _orderIdsToCleanup.Add(order.Id);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await InsertNotificationLogAsync(order.Id);

        // Assert — one log row was created, nothing skipped
        Assert.Empty(skippedNotificationIds);

        // Assert — fields sourced from orderschain must all be null for a standalone order
        NotificationLogEntry? entry = await PostgreUtil.GetNotificationLogEntry(order.Id);
        Assert.NotNull(entry);

        Assert.Equal(order.Id, entry.ShipmentId);
        Assert.Null(entry.OrderChainId);
        Assert.Null(entry.DialogId);
        Assert.Null(entry.TransmissionId);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_CalledTwiceForSameShipment_SecondCallReturnsSkippedIdAndInsertsNoDuplicateRow()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateCronJob: true, simulateConsumers: true, resultType: EmailNotificationResultType.Delivered);

        _orderIdsToCleanup.Add(order.Id);

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act — call twice for the same shipment, simulating the order being processed more than once
        IReadOnlyList<Guid> firstCallSkippedIds = await NotificationLogRepository.InsertNotificationLogEntry(order.Id, connection, transaction);
        IReadOnlyList<Guid> secondCallSkippedIds = await NotificationLogRepository.InsertNotificationLogEntry(order.Id, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert — first call logged the notification, second call reported the same notification as already logged
        Assert.Empty(firstCallSkippedIds);
        Guid skippedId = Assert.Single(secondCallSkippedIds);
        Assert.Equal(emailNotification.Id, skippedId);

        // Assert — no duplicate row was inserted
        int notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        Assert.Equal(1, notificationLogCount);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_ShipmentWithMultipleNotifications_InsertsOneRowPerNotification()
    {
        // Arrange — simulates an organization resolving to multiple recipients/channels on the same shipment.
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        EmailNotification firstEmail = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Delivered, "first@example.com");
        EmailNotification secondEmail = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Delivered, "second@example.com");
        SmsNotification sms = await AddSmsNotificationAsync(orderId, SmsNotificationResultType.Delivered);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await InsertNotificationLogAsync(orderId);

        // Assert — exactly one row per notification (own id, channel and destination), none skipped
        Assert.Empty(skippedNotificationIds);
        await AssertLoggedAsync(orderId, Expected(firstEmail, "Delivered"), Expected(secondEmail, "Delivered"), Expected(sms, "Delivered"));
    }

    [Fact]
    public async Task InsertNotificationLogEntry_ShipmentWithPartiallyLoggedNotifications_SkipsOnlyThePreviouslyLoggedOnes()
    {
        // Arrange — two notifications get logged first, then a third notification is added to the
        // same shipment afterwards (e.g. a late-arriving recipient), simulating a retried call that
        // should only skip the notifications it has already seen.
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        EmailNotification firstEmail = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Delivered, "first@example.com");
        EmailNotification secondEmail = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Delivered, "second@example.com");

        // First call: logs both existing notifications.
        Assert.Empty(await InsertNotificationLogAsync(orderId));

        // A third notification arrives on the same shipment after the first log call.
        EmailNotification thirdEmail = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Delivered, "third@example.com");

        // Act — retry for the same shipment: the first two are now duplicates, the third is genuinely new.
        IReadOnlyList<Guid> secondCallSkippedIds = await InsertNotificationLogAsync(orderId);

        // Assert — only the two already-logged notifications are reported as skipped, not the new third one.
        Assert.Equal(new HashSet<Guid> { firstEmail.Id, secondEmail.Id }, secondCallSkippedIds.ToHashSet());

        // Assert — the third notification was actually inserted, bringing the total to 3 rows.
        await AssertLoggedAsync(orderId, Expected(firstEmail, "Delivered"), Expected(secondEmail, "Delivered"), Expected(thirdEmail, "Delivered"));
    }

    [Fact]
    public async Task InsertNotificationLogEntry_EmailDeliveredAndSmsAccepted_LogsOnlyTheEmail()
    {
        // Arrange
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        EmailNotification email = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Delivered);
        SmsNotification sms = await AddSmsNotificationAsync(orderId, SmsNotificationResultType.Accepted);

        // Act
        IReadOnlyList<Guid> skippedIds = await InsertNotificationLogAsync(orderId);

        // Assert
        Assert.Empty(skippedIds);
        await AssertLoggedAsync(orderId, Expected(email, "Delivered"));
        Assert.DoesNotContain(sms.Id, (await GetLoggedAsync(orderId)).Keys);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_SmsDeliveredAndEmailSucceeded_LogsOnlyTheSms()
    {
        // Arrange
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        EmailNotification email = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Succeeded);
        SmsNotification sms = await AddSmsNotificationAsync(orderId, SmsNotificationResultType.Delivered);

        // Act
        IReadOnlyList<Guid> skippedIds = await InsertNotificationLogAsync(orderId);

        // Assert
        Assert.Empty(skippedIds);
        await AssertLoggedAsync(orderId, Expected(sms, "Delivered"));
        Assert.DoesNotContain(email.Id, (await GetLoggedAsync(orderId)).Keys);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_AllNotificationsPending_LogsNothingAndSkipsNothing()
    {
        // Arrange
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Sending);
        await AddSmsNotificationAsync(orderId, SmsNotificationResultType.New);

        // Act
        IReadOnlyList<Guid> skippedIds = await InsertNotificationLogAsync(orderId);

        // Assert
        Assert.Empty(skippedIds);
        await AssertLoggedAsync(orderId);
        Assert.Equal(0, await PostgreUtil.SelectNotificationLogEntryCount(orderId));
    }

    [Fact]
    public async Task InsertNotificationLogEntry_FailedAndExpiredResults_AreLogged()
    {
        // Arrange
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        EmailNotification email = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Failed_TTL);
        SmsNotification sms = await AddSmsNotificationAsync(orderId, SmsNotificationResultType.Failed_RecipientReserved);

        // Act
        IReadOnlyList<Guid> skippedIds = await InsertNotificationLogAsync(orderId);

        // Assert
        Assert.Empty(skippedIds);
        await AssertLoggedAsync(orderId, Expected(email, "Failed_TTL"), Expected(sms, "Failed_RecipientReserved"));
    }

    [Fact]
    public async Task InsertNotificationLogEntry_PendingNotificationBecomesFinal_LogsItWithoutDuplicatingOrOverwritingEarlierEntry()
    {
        // Arrange
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        EmailNotification email = await AddEmailNotificationAsync(orderId, EmailNotificationResultType.Delivered);
        SmsNotification sms = await AddSmsNotificationAsync(orderId, SmsNotificationResultType.Accepted);

        IReadOnlyList<Guid> firstSkippedIds = await InsertNotificationLogAsync(orderId);
        Assert.Empty(firstSkippedIds);

        await AssertLoggedAsync(orderId, Expected(email, "Delivered"));

        await PostgreUtil.UpdateNotificationResult<SmsNotification>(orderId, "Failed_TTL");

        // Act
        IReadOnlyList<Guid> skippedIds = await InsertNotificationLogAsync(orderId);

        // Assert — only the already-logged email is reported as skipped, its entry is unchanged, and the sms is now logged
        Guid skippedId = Assert.Single(skippedIds);
        Assert.Equal(email.Id, skippedId);

        await AssertLoggedAsync(orderId, Expected(email, "Delivered"), Expected(sms, "Failed_TTL"));
        Assert.Equal(2, await PostgreUtil.SelectNotificationLogEntryCount(orderId));
    }

    [Fact]
    public async Task InsertNotificationLogEntry_EmailResultTypes_AreLoggedExactlyWhenFinal()
    {
        // Arrange
        HashSet<string> persistedLabels = await PostgreUtil.GetEnumLabels("emailnotificationresulttype");
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        var expectedLogged = new Dictionary<Guid, LoggedEntry>();
        var expectedSkipped = new List<string>();

        foreach (EmailNotificationResultType result in Enum.GetValues<EmailNotificationResultType>().Where(r => persistedLabels.Contains(r.ToString())))
        {
            EmailNotification notification = await AddEmailNotificationAsync(orderId, result);
            if (result.IsFinal())
            {
                KeyValuePair<Guid, LoggedEntry> expected = Expected(notification, result.ToString());
                expectedLogged.Add(expected.Key, expected.Value);
            }
            else
            {
                expectedSkipped.Add(result.ToString());
            }
        }

        // Act
        IReadOnlyList<Guid> skippedIds = await InsertNotificationLogAsync(orderId);

        // Assert
        Assert.Empty(skippedIds);
        await AssertExactlyFinalResultsLoggedAsync(orderId, expectedLogged, expectedSkipped);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_SmsResultTypes_AreLoggedExactlyWhenFinal()
    {
        // Arrange
        HashSet<string> persistedLabels = await PostgreUtil.GetEnumLabels("smsnotificationresulttype");
        Guid orderId = await CreateEmailAndSmsOrderAsync();
        var expectedLogged = new Dictionary<Guid, LoggedEntry>();
        var expectedSkipped = new List<string>();

        foreach (SmsNotificationResultType result in Enum.GetValues<SmsNotificationResultType>().Where(r => persistedLabels.Contains(r.ToString())))
        {
            SmsNotification notification = await AddSmsNotificationAsync(orderId, result);
            if (result.IsFinal())
            {
                KeyValuePair<Guid, LoggedEntry> expected = Expected(notification, result.ToString());
                expectedLogged.Add(expected.Key, expected.Value);
            }
            else
            {
                expectedSkipped.Add(result.ToString());
            }
        }

        // Act
        IReadOnlyList<Guid> skippedIds = await InsertNotificationLogAsync(orderId);

        // Assert
        Assert.Empty(skippedIds);
        await AssertExactlyFinalResultsLoggedAsync(orderId, expectedLogged, expectedSkipped);
    }

    [Fact]
    public async Task GetByDialogId_WhenDialogIdMatches_ReturnsNotificationLogEntry()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        string operationId = Guid.NewGuid().ToString();

        DateTime requestedSendTime = DateTime.UtcNow;
        string sendersReference = $"ref-{Guid.NewGuid():N}";

        await RegisterShipmentWithDeliveredEmailNotification(
            orderId,
            orderChainId,
            notificationId,
            operationId,
            dialogId,
            transmissionId,
            requestedSendTime,
            sendersReference);

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken);

        // Assert
        NotificationLogSummary entry = Assert.Single(notificationLog);

        Assert.Equal("Email", entry.Channel);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(notificationId, entry.NotificationId);
        Assert.Equal("recipient@altinnxyz.no", entry.Destination);

        Assert.True(
            Math.Abs((requestedSendTime - entry.RequestedSendTime).Ticks) <= 500,
            $"Expected RequestedSendTime to be within 500 ticks of {requestedSendTime:O}, but was {entry.RequestedSendTime:O}.");
    }

    [Fact]
    public async Task GetByTransmissionId_WhenTransmissionIdMatches_ReturnsNotificationLogEntry()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        string operationId = Guid.NewGuid().ToString();
        string sendersReference = $"ref-{Guid.NewGuid():N}";
        DateTime requestedSendTime = DateTime.UtcNow;

        await RegisterShipmentWithDeliveredEmailNotification(
            orderId,
            orderChainId,
            notificationId,
            operationId,
            dialogId,
            transmissionId,
            requestedSendTime,
            sendersReference);

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        NotificationLogSummary entry = Assert.Single(notificationLog);

        Assert.Equal("Email", entry.Channel);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(notificationId, entry.NotificationId);
        Assert.Equal("recipient@altinnxyz.no", entry.Destination);

        Assert.True(
            Math.Abs((requestedSendTime - entry.RequestedSendTime).Ticks) <= 500,
            $"Expected RequestedSendTime to be within 500 ticks of {requestedSendTime:O}, but was {entry.RequestedSendTime:O}.");
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenBothIdsMatch_ReturnsNotificationLogEntry()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        string operationId = Guid.NewGuid().ToString();
        string sendersReference = $"ref-{Guid.NewGuid():N}";
        DateTime requestedSendTime = DateTime.UtcNow;

        await RegisterShipmentWithDeliveredSmsNotification(
            orderId,
            orderChainId,
            notificationId,
            operationId,
            dialogId,
            transmissionId,
            requestedSendTime,
            sendersReference);

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken);

        // Assert
        NotificationLogSummary entry = Assert.Single(notificationLog);

        Assert.Equal("Sms", entry.Channel);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal("+4799999999", entry.Destination);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(notificationId, entry.NotificationId);

        Assert.True(
            Math.Abs((requestedSendTime - entry.RequestedSendTime).Ticks) <= 500,
            $"Expected RequestedSendTime to be within 500 ticks of {requestedSendTime:O}, but was {entry.RequestedSendTime:O}.");
    }

    [Fact]
    public async Task GetByDialogId_WhenDialogIdDoesNotMatch_ReturnsEmptyList()
    {
        // Arrange
        NotificationLogRepository repository = GetNotificationLogRepository();

        // Act
        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogId(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(notificationLog);
    }

    [Fact]
    public async Task GetByTransmissionId_WhenTransmissionIdDoesNotMatch_ReturnsEmptyList()
    {
        // Arrange
        NotificationLogRepository repository = GetNotificationLogRepository();

        // Act
        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByTransmissionId(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(notificationLog);
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenDialogIdDoesNotMatch_ReturnsEmptyList()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        string operationId = Guid.NewGuid().ToString();

        await RegisterShipmentWithDeliveredEmailNotification(
            orderId,
            orderChainId,
            notificationId,
            operationId,
            dialogId,
            transmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogAndTransmissionIds(Guid.NewGuid().ToString(), transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(notificationLog);
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenTransmissionIdDoesNotMatch_ReturnsEmptyList()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        string operationId = Guid.NewGuid().ToString();

        await RegisterShipmentWithDeliveredEmailNotification(
            orderId,
            orderChainId,
            notificationId,
            operationId,
            dialogId,
            transmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogAndTransmissionIds(dialogId, Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(notificationLog);
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenDialogAndTransmissionBelongToDifferentEntries_ReturnsEmptyList()
    {
        // Arrange
        Guid firstOrderId = Guid.NewGuid();
        Guid firstOrderChainId = Guid.NewGuid();
        Guid firstNotificationId = Guid.NewGuid();

        Guid secondOrderId = Guid.NewGuid();
        Guid secondOrderChainId = Guid.NewGuid();
        Guid secondNotificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(firstOrderId);
        _orderIdsToCleanup.Add(secondOrderId);
        _orderChainIdsToCleanup.Add(firstOrderChainId);
        _orderChainIdsToCleanup.Add(secondOrderChainId);

        string firstDialogId = Guid.NewGuid().ToString();
        string firstTransmissionId = Guid.NewGuid().ToString();
        string secondDialogId = Guid.NewGuid().ToString();
        string secondTransmissionId = Guid.NewGuid().ToString();

        await RegisterShipmentWithDeliveredEmailNotification(
            firstOrderId,
            firstOrderChainId,
            firstNotificationId,
            Guid.NewGuid().ToString(),
            firstDialogId,
            firstTransmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        await RegisterShipmentWithDeliveredEmailNotification(
            secondOrderId,
            secondOrderChainId,
            secondNotificationId,
            Guid.NewGuid().ToString(),
            secondDialogId,
            secondTransmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogAndTransmissionIds(firstDialogId, secondTransmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(notificationLog);
    }

    [Fact]
    public async Task GetByDialogId_WhenMatchingAndNonMatchingEntriesExist_ReturnsOnlyMatchingEntries()
    {
        // Arrange
        Guid matchingOrderId = Guid.NewGuid();
        Guid matchingOrderChainId = Guid.NewGuid();
        Guid matchingNotificationId = Guid.NewGuid();

        Guid nonMatchingOrderId = Guid.NewGuid();
        Guid nonMatchingOrderChainId = Guid.NewGuid();
        Guid nonMatchingNotificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(matchingOrderId);
        _orderIdsToCleanup.Add(nonMatchingOrderId);
        _orderChainIdsToCleanup.Add(matchingOrderChainId);
        _orderChainIdsToCleanup.Add(nonMatchingOrderChainId);

        string matchingDialogId = Guid.NewGuid().ToString();
        string nonMatchingDialogId = Guid.NewGuid().ToString();

        await RegisterShipmentWithDeliveredEmailNotification(
            matchingOrderId,
            matchingOrderChainId,
            matchingNotificationId,
            Guid.NewGuid().ToString(),
            matchingDialogId,
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        await RegisterShipmentWithDeliveredEmailNotification(
            nonMatchingOrderId,
            nonMatchingOrderChainId,
            nonMatchingNotificationId,
            Guid.NewGuid().ToString(),
            nonMatchingDialogId,
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogId(
                matchingDialogId,
                TestContext.Current.CancellationToken);

        // Assert
        NotificationLogSummary entry = Assert.Single(notificationLog);
        Assert.Equal(matchingNotificationId, entry.NotificationId);
        Assert.Equal(matchingDialogId, entry.DialogId);
    }

    [Fact]
    public async Task GetByTransmissionId_WhenMatchingAndNonMatchingEntriesExist_ReturnsOnlyMatchingEntries()
    {
        // Arrange
        Guid matchingOrderId = Guid.NewGuid();
        Guid matchingOrderChainId = Guid.NewGuid();
        Guid matchingNotificationId = Guid.NewGuid();

        Guid nonMatchingOrderId = Guid.NewGuid();
        Guid nonMatchingOrderChainId = Guid.NewGuid();
        Guid nonMatchingNotificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(matchingOrderId);
        _orderIdsToCleanup.Add(nonMatchingOrderId);
        _orderChainIdsToCleanup.Add(matchingOrderChainId);
        _orderChainIdsToCleanup.Add(nonMatchingOrderChainId);

        string matchingTransmissionId = Guid.NewGuid().ToString();
        string nonMatchingTransmissionId = Guid.NewGuid().ToString();

        await RegisterShipmentWithDeliveredEmailNotification(
            matchingOrderId,
            matchingOrderChainId,
            matchingNotificationId,
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            matchingTransmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        await RegisterShipmentWithDeliveredEmailNotification(
            nonMatchingOrderId,
            nonMatchingOrderChainId,
            nonMatchingNotificationId,
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            nonMatchingTransmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByTransmissionId(
                matchingTransmissionId,
                TestContext.Current.CancellationToken);

        // Assert
        NotificationLogSummary entry = Assert.Single(notificationLog);
        Assert.Equal(matchingNotificationId, entry.NotificationId);
        Assert.Equal(matchingTransmissionId, entry.TransmissionId);
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenMatchingAndNonMatchingEntriesExist_ReturnsOnlyExactMatches()
    {
        // Arrange
        Guid matchingOrderId = Guid.NewGuid();
        Guid matchingOrderChainId = Guid.NewGuid();
        Guid matchingNotificationId = Guid.NewGuid();

        Guid sameDialogDifferentTransmissionOrderId = Guid.NewGuid();
        Guid sameDialogDifferentTransmissionOrderChainId = Guid.NewGuid();
        Guid sameDialogDifferentTransmissionNotificationId = Guid.NewGuid();

        Guid sameTransmissionDifferentDialogOrderId = Guid.NewGuid();
        Guid sameTransmissionDifferentDialogOrderChainId = Guid.NewGuid();
        Guid sameTransmissionDifferentDialogNotificationId = Guid.NewGuid();

        _orderIdsToCleanup.Add(matchingOrderId);
        _orderIdsToCleanup.Add(sameDialogDifferentTransmissionOrderId);
        _orderIdsToCleanup.Add(sameTransmissionDifferentDialogOrderId);

        _orderChainIdsToCleanup.Add(matchingOrderChainId);
        _orderChainIdsToCleanup.Add(sameDialogDifferentTransmissionOrderChainId);
        _orderChainIdsToCleanup.Add(sameTransmissionDifferentDialogOrderChainId);

        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        await RegisterShipmentWithDeliveredEmailNotification(
            matchingOrderId,
            matchingOrderChainId,
            matchingNotificationId,
            Guid.NewGuid().ToString(),
            dialogId,
            transmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        await RegisterShipmentWithDeliveredEmailNotification(
            sameDialogDifferentTransmissionOrderId,
            sameDialogDifferentTransmissionOrderChainId,
            sameDialogDifferentTransmissionNotificationId,
            Guid.NewGuid().ToString(),
            dialogId,
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        await RegisterShipmentWithDeliveredEmailNotification(
            sameTransmissionDifferentDialogOrderId,
            sameTransmissionDifferentDialogOrderChainId,
            sameTransmissionDifferentDialogNotificationId,
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            transmissionId,
            DateTime.UtcNow,
            $"ref-{Guid.NewGuid():N}");

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();

        IImmutableList<NotificationLogSummary> notificationLog =
            await repository.GetByDialogAndTransmissionIds(
                dialogId,
                transmissionId,
                TestContext.Current.CancellationToken);

        // Assert
        NotificationLogSummary entry = Assert.Single(notificationLog);
        Assert.Equal(matchingNotificationId, entry.NotificationId);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal(transmissionId, entry.TransmissionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByDialogId_WhenDialogIdIsEmptyOrWhitespace_ThrowsArgumentException(string dialogId)
    {
        // Arrange
        NotificationLogRepository repository = GetNotificationLogRepository();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetByDialogId(
                dialogId,
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByTransmissionId_WhenTransmissionIdIsEmptyOrWhitespace_ThrowsArgumentException(string transmissionId)
    {
        // Arrange
        NotificationLogRepository repository = GetNotificationLogRepository();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetByTransmissionId(
                transmissionId,
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("", "valid-transmission-id")]
    [InlineData("   ", "valid-transmission-id")]
    [InlineData("valid-dialog-id", "")]
    [InlineData("valid-dialog-id", "   ")]
    public async Task GetByDialogAndTransmissionIds_WhenAnIdIsEmptyOrWhitespace_ThrowsArgumentException(string dialogId, string transmissionId)
    {
        // Arrange
        NotificationLogRepository repository = GetNotificationLogRepository();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetByDialogAndTransmissionIds(
                dialogId,
                transmissionId,
                TestContext.Current.CancellationToken));
    }

    private static KeyValuePair<Guid, LoggedEntry> Expected(EmailNotification notification, string status) =>
        new(notification.Id, ("Email", notification.Recipient.ToAddress!, status));

    private static KeyValuePair<Guid, LoggedEntry> Expected(SmsNotification notification, string status) =>
        new(notification.Id, ("Sms", notification.Recipient.MobileNumber!, status));

    private static async Task<Dictionary<Guid, LoggedEntry>> GetLoggedAsync(Guid shipmentId)
    {
        List<NotificationLogEntry> entries = await PostgreUtil.GetNotificationLogEntries(shipmentId);
        return entries.ToDictionary(e => e.NotificationId, e => (e.Channel, e.Destination, e.Status));
    }

    private static async Task AssertLoggedAsync(Guid shipmentId, params KeyValuePair<Guid, LoggedEntry>[] expected)
    {
        Assert.Equal(expected.ToDictionary(e => e.Key, e => e.Value), await GetLoggedAsync(shipmentId));
    }

    private static async Task AssertExactlyFinalResultsLoggedAsync(Guid shipmentId, Dictionary<Guid, LoggedEntry> expectedLogged, List<string> expectedSkipped)
    {
        // Guard against a vacuous pass: both sides of the final/pending split must be exercised.
        Assert.NotEmpty(expectedLogged);
        Assert.NotEmpty(expectedSkipped);

        Dictionary<Guid, LoggedEntry> logged = await GetLoggedAsync(shipmentId);
        Assert.True(
            expectedLogged.Count == logged.Count && expectedLogged.All(e => logged.TryGetValue(e.Key, out LoggedEntry actual) && actual == e.Value),
            $"Database and domain disagree on final results. Expected logged: [{string.Join(", ", expectedLogged.Values.Select(v => v.Status).Order())}]. Actual logged: [{string.Join(", ", logged.Values.Select(v => v.Status).Order())}]. Expected not logged: [{string.Join(", ", expectedSkipped.Order())}].");
    }

    private static async Task<IReadOnlyList<Guid>> InsertNotificationLogAsync(Guid shipmentId)
    {
        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<Guid> skippedIds = await NotificationLogRepository.InsertNotificationLogEntry(shipmentId, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return skippedIds;
    }

    private async Task<Guid> CreateEmailAndSmsOrderAsync()
    {
        OrderRepository orderRepo = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            Creator = new("ttd"),
            SendersReference = $"log-final-result-{Guid.NewGuid():N}",
            Type = OrderType.Notification,
            Templates =
            [
                new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain),
                new SmsTemplate("Altinn", "sms-body")
            ]
        };

        _orderIdsToCleanup.Add(order.Id);
        await orderRepo.Create(order);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);

        return order.Id;
    }

    private static async Task<EmailNotification> AddEmailNotificationAsync(Guid orderId, EmailNotificationResultType result, string? toAddress = null)
    {
        EmailNotificationRepository emailRepo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        var notification = new EmailNotification
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            RequestedSendTime = DateTime.UtcNow,
            Recipient = new() { ToAddress = toAddress ?? $"{Guid.NewGuid():N}@example.com" },
            SendResult = new(result, DateTime.UtcNow)
        };

        await emailRepo.AddNotification(notification, DateTime.UtcNow.AddDays(1));
        return notification;
    }

    private static async Task<SmsNotification> AddSmsNotificationAsync(Guid orderId, SmsNotificationResultType result, string mobileNumber = "+4791111111")
    {
        SmsNotificationRepository smsRepo = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        var notification = new SmsNotification
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            RequestedSendTime = DateTime.UtcNow,
            Recipient = new() { MobileNumber = mobileNumber },
            SendResult = new(result, DateTime.UtcNow)
        };

        await smsRepo.AddNotification(notification, DateTime.UtcNow.AddDays(1));
        return notification;
    }

    private static async Task AssertPersistedEntryAsync(
        Guid orderId,
        Guid orderChainId,
        string creatorName,
        Guid dialogId,
        string transmissionId,
        string resourceId,
        string channel,
        string destination,
        string deliveryReference)
    {
        NotificationLogEntry? entry = await PostgreUtil.GetNotificationLogEntry(orderId);
        Assert.NotNull(entry);

        Assert.Equal(orderId, entry.ShipmentId);
        Assert.NotEqual(Guid.Empty, entry.NotificationId);
        Assert.Equal(orderChainId, entry.OrderChainId);
        Assert.Equal(creatorName, entry.CreatorName);
        Assert.Equal(dialogId.ToString(), entry.DialogId);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(destination, entry.Destination);
        Assert.Equal(resourceId, entry.Resource);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal(channel, entry.Channel);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal(deliveryReference, entry.DeliveryReference);
        Assert.Null(entry.Recipient);

        // RequestedSendTime is sourced from the order's own requested send timestamp, set moments ago by the test setup.
        Assert.True(entry.RequestedSendTime <= DateTime.UtcNow && entry.RequestedSendTime > DateTime.UtcNow.AddMinutes(-1));

        // LastUpdateTime is set by the database itself (resulttime) when the notification was marked Delivered,
        // so it can't be asserted against an exact expected value — only that it's populated and not before the request was made.
        Assert.True(entry.LastUpdateTime >= entry.RequestedSendTime);
    }

    private static NotificationLogRepository GetNotificationLogRepository()
    {
        return (NotificationLogRepository)ServiceUtil
            .GetServices([typeof(INotificationLogRepository)])
            .First(i => i.GetType() == typeof(NotificationLogRepository));
    }

    private static async Task RegisterShipmentWithDeliveredSmsNotification(
        Guid orderId,
        Guid orderChainId,
        Guid smsNotificationId,
        string gatewayReference,
        string dialogId,
        string transmissionId,
        DateTime requestedSendTime,
        string sendersReference)
    {
        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("ttd"))
            .SetType(OrderType.Notification)
            .SetSendersReference(sendersReference)
            .SetRequestedSendTime(requestedSendTime)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetDialogportenAssociation(new DialogportenIdentifiers
            {
                DialogId = dialogId,
                TransmissionId = transmissionId,
            })
            .SetRecipient(new NotificationRecipient
            {
                RecipientSms = new RecipientSms
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptions
                    {
                        Sender = "Altinn",
                        Body = "Your OTP is 12345",
                        SendingTimePolicy = SendingTimePolicy.Daytime
                    }
                }
            })
            .Build();

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new("ttd"),
            Created = DateTime.UtcNow,
            Type = OrderType.Notification,
            SendersReference = sendersReference,
            RequestedSendTime = requestedSendTime,
            SmsSendingTimePolicy = SendingTimePolicy.Daytime,
            NotificationChannel = NotificationChannel.Sms,
            Recipients = [new Altinn.Notifications.Core.Models.Recipient([new SmsAddressPoint("+4799999999")])],
            Templates =
            [
                new SmsTemplate("Altinn", "Your OTP is 12345")
            ]
        };

        SmsNotification smsNotification = new()
        {
            OrderId = orderId,
            Id = smsNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new() { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        OrderRepository orderRepository = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        await orderRepository.Create(
            orderChainRequest,
            notificationOrder,
            null,
            TestContext.Current.CancellationToken);

        await orderRepository.SetProcessingStatus(
            orderId,
            OrderProcessingStatus.Processing);

        SmsNotificationRepository smsNotificationRepository = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        await smsNotificationRepository.AddNotification(
            smsNotification,
            DateTime.UtcNow.AddDays(2));

        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await smsNotificationRepository.UpdateSendStatusAsync(
            unitOfWork,
            smsNotificationId,
            SmsNotificationResultType.Sending);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await smsNotificationRepository.UpdateSendStatusAsync(
            unitOfWork,
            smsNotificationId,
            SmsNotificationResultType.Accepted,
            gatewayReference);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await smsNotificationRepository.UpdateSendStatusAsync(
            unitOfWork,
            null,
            SmsNotificationResultType.Delivered,
            gatewayReference);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
    }

    private static async Task RegisterShipmentWithDeliveredEmailNotification(
        Guid orderId,
        Guid orderChainId,
        Guid emailNotificationId,
        string operationId,
        string dialogId,
        string transmissionId,
        DateTime requestedSendTime,
        string sendersReference)
    {
        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("ttd"))
            .SetType(OrderType.Notification)
            .SetSendersReference(sendersReference)
            .SetRequestedSendTime(requestedSendTime)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetDialogportenAssociation(new DialogportenIdentifiers
            {
                DialogId = dialogId,
                TransmissionId = transmissionId,
            })
            .SetRecipient(new NotificationRecipient
            {
                RecipientEmail = new RecipientEmail
                {
                    EmailAddress = "recipient@altinnxyz.no",
                    Settings = new EmailSendingOptions
                    {
                        Subject = "Decision from Altinn",
                        ContentType = EmailContentType.Plain,
                        SenderEmailAddress = "noreply@altinn.no",
                        Body = "Please review the attached document",
                        SendingTimePolicy = SendingTimePolicy.Anytime
                    }
                }
            })
            .Build();

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new("ttd"),
            Created = DateTime.UtcNow,
            Type = OrderType.Notification,
            SendersReference = sendersReference,
            RequestedSendTime = requestedSendTime,
            SmsSendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Email,
            Recipients = [new Altinn.Notifications.Core.Models.Recipient([new EmailAddressPoint("recipient@altinnxyz.no")])],
            Templates =
            [
                new EmailTemplate(
                    "noreply@altinn.no",
                    "Decision from Altinn",
                    "Please review the attached document.",
                    EmailContentType.Plain)
            ]
        };

        EmailNotification emailNotification = new()
        {
            OrderId = orderId,
            Id = emailNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new() { ToAddress = "recipient@altinnxyz.no" },
            SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow)
        };

        OrderRepository orderRepository = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        await orderRepository.Create(
            orderChainRequest,
            notificationOrder,
            null,
            TestContext.Current.CancellationToken);

        await orderRepository.SetProcessingStatus(
            orderId,
            OrderProcessingStatus.Processing);

        EmailNotificationRepository emailNotificationRepository = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        await emailNotificationRepository.AddNotification(
            emailNotification,
            DateTime.UtcNow.AddDays(2));

        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await emailNotificationRepository.UpdateSendStatusAsync(
            unitOfWork,
            emailNotificationId,
            EmailNotificationResultType.Sending);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await emailNotificationRepository.UpdateSendStatusAsync(
            unitOfWork,
            emailNotificationId,
            EmailNotificationResultType.Succeeded,
            operationId);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await emailNotificationRepository.UpdateSendStatusAsync(
            unitOfWork,
            null,
            EmailNotificationResultType.Delivered,
            operationId);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
    }
}
