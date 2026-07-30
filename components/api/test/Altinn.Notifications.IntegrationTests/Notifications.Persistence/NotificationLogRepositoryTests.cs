using Altinn.Notifications.Core.Enums;
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

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class NotificationLogRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToCleanup = [];
    private readonly List<Guid> _orderChainIdsToCleanup = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (Guid orderId in _orderIdsToCleanup)
        {
            await PostgreUtil.DeleteNotificationLogFromDb(orderId);
            await PostgreUtil.DeleteOrderFromDb(orderId);
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

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await NotificationLogRepository.InsertNotificationLogEntry(orderId, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert — nothing was skipped
        Assert.Empty(skippedNotificationIds);

        // Assert — persisted properties
        NotificationLogEntry? entry = await PostgreUtil.GetNotificationLogEntry(orderId);
        Assert.NotNull(entry);

        Assert.Equal(orderId, entry.ShipmentId);
        Assert.NotEqual(Guid.Empty, entry.NotificationId);
        Assert.Equal(orderChainId, entry.OrderChainId);
        Assert.Equal(creatorName, entry.CreatorName);
        Assert.Equal(dialogId.ToString(), entry.DialogId);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(toAddress, entry.Destination);
        Assert.Equal(resourceId, entry.Resource);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal("Email", entry.Channel);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal(operationId, entry.DeliveryReference);  // set via updateemailnotification_v4
        Assert.Null(entry.Recipient);

        // RequestedSendTime is sourced from the order's own requested send timestamp, set moments ago by the test setup.
        Assert.True(entry.RequestedSendTime <= DateTime.UtcNow && entry.RequestedSendTime > DateTime.UtcNow.AddMinutes(-1));

        // LastUpdateTime is set by the database itself (resulttime) when the notification was marked Delivered,
        // so it can't be asserted against an exact expected value — only that it's populated and not before the request was made.
        Assert.True(entry.LastUpdateTime >= entry.RequestedSendTime);
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

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await NotificationLogRepository.InsertNotificationLogEntry(orderId, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert — nothing was skipped
        Assert.Empty(skippedNotificationIds);

        // Assert — persisted properties
        NotificationLogEntry? entry = await PostgreUtil.GetNotificationLogEntry(orderId);
        Assert.NotNull(entry);

        Assert.Equal(orderId, entry.ShipmentId);
        Assert.NotEqual(Guid.Empty, entry.NotificationId);
        Assert.Equal(orderChainId, entry.OrderChainId);
        Assert.Equal(creatorName, entry.CreatorName);
        Assert.Equal(dialogId.ToString(), entry.DialogId);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(mobileNumber, entry.Destination);
        Assert.Equal(resourceId, entry.Resource);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal("Sms", entry.Channel);
        Assert.Equal("Delivered", entry.Status);
        Assert.Null(entry.Recipient);
        Assert.Equal(gatewayReference, entry.DeliveryReference);  // SMS path — gateway reference is set

        // RequestedSendTime is sourced from the order's own requested send timestamp, set moments ago by the test setup.
        Assert.True(entry.RequestedSendTime <= DateTime.UtcNow && entry.RequestedSendTime > DateTime.UtcNow.AddMinutes(-1));

        // LastUpdateTime is set by the database itself (resulttime) when the notification was marked Delivered,
        // so it can't be asserted against an exact expected value — only that it's populated and not before the request was made.
        Assert.True(entry.LastUpdateTime >= entry.RequestedSendTime);
    }

    [Fact]
    public async Task InsertNotificationLogEntry_WithStandaloneOrder_ChainDerivedFieldsAreNull()
    {
        // Arrange — standalone order has no orderschain row, so orderchainid is NULL.
        (NotificationOrder order, EmailNotification _) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateCronJob: true, simulateConsumers: true);

        _orderIdsToCleanup.Add(order.Id);

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await NotificationLogRepository.InsertNotificationLogEntry(order.Id, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

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
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateCronJob: true, simulateConsumers: true);

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
        OrderRepository orderRepo = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        EmailNotificationRepository emailRepo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        SmsNotificationRepository smsRepo = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            Creator = new("ttd"),
            SendersReference = "tx-test-multi-notification-log",
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

        var firstEmail = new EmailNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "first@example.com" }, SendResult = new(EmailNotificationResultType.Delivered, DateTime.UtcNow) };
        var secondEmail = new EmailNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "second@example.com" }, SendResult = new(EmailNotificationResultType.Delivered, DateTime.UtcNow) };
        var sms = new SmsNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { MobileNumber = "+4791111111" }, SendResult = new(SmsNotificationResultType.Delivered, DateTime.UtcNow) };

        await emailRepo.AddNotification(firstEmail, DateTime.UtcNow.AddDays(1));
        await emailRepo.AddNotification(secondEmail, DateTime.UtcNow.AddDays(1));
        await smsRepo.AddNotification(sms, DateTime.UtcNow.AddDays(1));

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        IReadOnlyList<Guid> skippedNotificationIds = await NotificationLogRepository.InsertNotificationLogEntry(order.Id, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert — one row per notification, none skipped
        Assert.Empty(skippedNotificationIds);

        int notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        Assert.Equal(3, notificationLogCount);

        string emailChannelCountSql = $"SELECT COUNT(*) FROM notifications.notificationlog WHERE shipmentid = '{order.Id}' AND channel = 'Email'";
        int emailChannelCount = await PostgreUtil.RunSqlReturnOutput<int>(emailChannelCountSql);
        Assert.Equal(2, emailChannelCount);

        string smsChannelCountSql = $"SELECT COUNT(*) FROM notifications.notificationlog WHERE shipmentid = '{order.Id}' AND channel = 'Sms'";
        int smsChannelCount = await PostgreUtil.RunSqlReturnOutput<int>(smsChannelCountSql);
        Assert.Equal(1, smsChannelCount);

        // Each source notification's own alternateid must appear exactly once as notificationid
        foreach (var notificationId in new[] { firstEmail.Id, secondEmail.Id, sms.Id })
        {
            string notificationIdCountSql = $"SELECT COUNT(*) FROM notifications.notificationlog WHERE notificationid = '{notificationId}'";
            int notificationIdCount = await PostgreUtil.RunSqlReturnOutput<int>(notificationIdCountSql);
            Assert.Equal(1, notificationIdCount);
        }
    }

    [Fact]
    public async Task InsertNotificationLogEntry_ShipmentWithPartiallyLoggedNotifications_SkipsOnlyThePreviouslyLoggedOnes()
    {
        // Arrange — two notifications get logged first, then a third notification is added to the
        // same shipment afterwards (e.g. a late-arriving recipient), simulating a retried call that
        // should only skip the notifications it has already seen.
        OrderRepository orderRepo = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        EmailNotificationRepository emailRepo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            Creator = new("ttd"),
            SendersReference = "tx-test-partial-idempotency",
            Type = OrderType.Notification,
            Templates = [new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain)]
        };

        _orderIdsToCleanup.Add(order.Id);
        await orderRepo.Create(order);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);

        var firstEmail = new EmailNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "first@example.com" }, SendResult = new(EmailNotificationResultType.Delivered, DateTime.UtcNow) };
        var secondEmail = new EmailNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "second@example.com" }, SendResult = new(EmailNotificationResultType.Delivered, DateTime.UtcNow) };

        await emailRepo.AddNotification(firstEmail, DateTime.UtcNow.AddDays(1));
        await emailRepo.AddNotification(secondEmail, DateTime.UtcNow.AddDays(1));

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;

        // First call: logs both existing notifications.
        await using (var firstConnection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var firstTransaction = await firstConnection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            IReadOnlyList<Guid> firstCallSkippedIds = await NotificationLogRepository.InsertNotificationLogEntry(order.Id, firstConnection, firstTransaction);
            await firstTransaction.CommitAsync(TestContext.Current.CancellationToken);
            Assert.Empty(firstCallSkippedIds);
        }

        // A third notification arrives on the same shipment after the first log call.
        var thirdEmail = new EmailNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "third@example.com" }, SendResult = new(EmailNotificationResultType.Delivered, DateTime.UtcNow) };
        await emailRepo.AddNotification(thirdEmail, DateTime.UtcNow.AddDays(1));

        // Act — retry for the same shipment: the first two are now duplicates, the third is genuinely new.
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        IReadOnlyList<Guid> secondCallSkippedIds = await NotificationLogRepository.InsertNotificationLogEntry(order.Id, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert — only the two already-logged notifications are reported as skipped, not the new third one.
        Assert.Equal(2, secondCallSkippedIds.Count);
        Assert.Contains(firstEmail.Id, secondCallSkippedIds);
        Assert.Contains(secondEmail.Id, secondCallSkippedIds);
        Assert.DoesNotContain(thirdEmail.Id, secondCallSkippedIds);

        // Assert — the third notification was actually inserted, bringing the total to 3 rows.
        int notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        Assert.Equal(3, notificationLogCount);

        string thirdNotificationCountSql = $"SELECT COUNT(*) FROM notifications.notificationlog WHERE notificationid = '{thirdEmail.Id}'";
        int thirdNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(thirdNotificationCountSql);
        Assert.Equal(1, thirdNotificationCount);
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByDialogId_ReturnsNotificationLogEntry()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        Guid emailNotificationId = Guid.NewGuid();
        string dialogId = Guid.NewGuid().ToString();
        DateTime requestedSendTime = DateTime.UtcNow;
        string operationId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        string sendersReference = $"ref-{Guid.NewGuid():N}";

        await RegisterShipmentWithDeliveredEmailNotification(
            orderId,
            orderChainId,
            emailNotificationId,
            operationId,
            dialogId,
            transmissionId,
            requestedSendTime,
            sendersReference);

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();
        IReadOnlyList<NotificationLogEntry> notificationLog =
            await repository.GetByDialogOrTransmission(transmissionId: string.Empty, dialogId: dialogId, TestContext.Current.CancellationToken);

        // Assert
        NotificationLogEntry entry = Assert.Single(notificationLog);

        Assert.Null(entry.Resource);
        Assert.Null(entry.Recipient);
        Assert.Equal("Email", entry.Channel);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal("ttd", entry.CreatorName);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal(orderId, entry.ShipmentId);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal(orderChainId, entry.OrderChainId);
        Assert.Equal(operationId, entry.DeliveryReference);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(sendersReference, entry.SendersReference);
        Assert.Equal(emailNotificationId, entry.NotificationId);
        Assert.Equal("recipient@altinnxyz.no", entry.Destination);
        Assert.True(Math.Abs((requestedSendTime - entry.RequestedSendTime).Ticks) <= 500);
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByTransmissionId_ReturnsNotificationLogEntry()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        Guid emailNotificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        DateTime requestedSendTime = DateTime.UtcNow;
        string sendersReference = $"ref-{Guid.NewGuid():N}";

        await RegisterShipmentWithDeliveredEmailNotification(
            orderId,
            orderChainId,
            emailNotificationId,
            operationId,
            dialogId,
            transmissionId,
            requestedSendTime,
            sendersReference);

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();
        IReadOnlyList<NotificationLogEntry> notificationLog =
            await repository.GetByDialogOrTransmission(transmissionId: transmissionId, dialogId: string.Empty, TestContext.Current.CancellationToken);

        // Assert
        NotificationLogEntry entry = Assert.Single(notificationLog);

        Assert.Null(entry.Resource);
        Assert.Null(entry.Recipient);
        Assert.Equal("Email", entry.Channel);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal("ttd", entry.CreatorName);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal(orderId, entry.ShipmentId);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal(orderChainId, entry.OrderChainId);
        Assert.Equal(operationId, entry.DeliveryReference);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(sendersReference, entry.SendersReference);
        Assert.Equal(emailNotificationId, entry.NotificationId);
        Assert.Equal("recipient@altinnxyz.no", entry.Destination);
        Assert.True(Math.Abs((requestedSendTime - entry.RequestedSendTime).Ticks) <= 500);
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByDialogIdAndTransmissionId_ReturnsNotificationLogEntry()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        Guid emailNotificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        DateTime requestedSendTime = DateTime.UtcNow;
        string sendersReference = $"ref-{Guid.NewGuid():N}";

        await RegisterShipmentWithDeliveredSmsNotification(
            orderId,
            orderChainId,
            emailNotificationId,
            operationId,
            dialogId,
            transmissionId,
            requestedSendTime,
            sendersReference);

        // Act
        NotificationLogRepository repository = GetNotificationLogRepository();
        IReadOnlyList<NotificationLogEntry> notificationLog =
            await repository.GetByDialogOrTransmission(transmissionId: transmissionId, dialogId: dialogId, TestContext.Current.CancellationToken);

        // Assert
        NotificationLogEntry entry = Assert.Single(notificationLog);

        Assert.Null(entry.Resource);
        Assert.Null(entry.Recipient);
        Assert.Equal("Sms", entry.Channel);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal("ttd", entry.CreatorName);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal(orderId, entry.ShipmentId);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal("+4799999999", entry.Destination);
        Assert.Equal(orderChainId, entry.OrderChainId);
        Assert.Equal(operationId, entry.DeliveryReference);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(sendersReference, entry.SendersReference);
        Assert.Equal(emailNotificationId, entry.NotificationId);
        Assert.True(Math.Abs((requestedSendTime - entry.RequestedSendTime).Ticks) <= 500);
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenDialogIdDoesNotMatch_ReturnsEmptyList()
    {
        NotificationLogRepository repository = GetNotificationLogRepository();

        IReadOnlyList<NotificationLogEntry> notificationLog =
            await repository.GetByDialogOrTransmission(transmissionId: string.Empty, dialogId: Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        Assert.Empty(notificationLog);
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenTransmissionIdDoesNotMatch_ReturnsEmptyList()
    {
        NotificationLogRepository repository = GetNotificationLogRepository();

        IReadOnlyList<NotificationLogEntry> notificationLog =
            await repository.GetByDialogOrTransmission(transmissionId: Guid.NewGuid().ToString(), dialogId: string.Empty, TestContext.Current.CancellationToken);

        Assert.Empty(notificationLog);
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenDialogIdAndTransmissionIdDoNotMatch_ReturnsEmptyList()
    {
        NotificationLogRepository repository = GetNotificationLogRepository();

        IReadOnlyList<NotificationLogEntry> notificationLog =
            await repository.GetByDialogOrTransmission(transmissionId: Guid.NewGuid().ToString(), dialogId: Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        Assert.Empty(notificationLog);
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
        string gatwayReference,
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
            SendingTimePolicy = SendingTimePolicy.Daytime,
            NotificationChannel = NotificationChannel.Sms,
            Recipients = [new Recipient([new SmsAddressPoint("+4799999999")])],
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

        await smsNotificationRepository.UpdateSendStatus(
            smsNotificationId,
            SmsNotificationResultType.Sending);

        await smsNotificationRepository.UpdateSendStatus(
            smsNotificationId,
            SmsNotificationResultType.Accepted,
            gatwayReference);

        await smsNotificationRepository.UpdateSendStatus(
            null,
            SmsNotificationResultType.Delivered,
            gatwayReference);
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
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Email,
            Recipients = [new Recipient([new EmailAddressPoint("recipient@altinnxyz.no")])],
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

        await emailNotificationRepository.UpdateSendStatus(
            emailNotificationId,
            EmailNotificationResultType.Sending);

        await emailNotificationRepository.UpdateSendStatus(
            emailNotificationId,
            EmailNotificationResultType.Succeeded,
            operationId);

        await emailNotificationRepository.UpdateSendStatus(
            null,
            EmailNotificationResultType.Delivered,
            operationId);
    }
}
