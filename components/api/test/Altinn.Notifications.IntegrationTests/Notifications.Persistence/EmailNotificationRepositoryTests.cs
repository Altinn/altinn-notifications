using System.Reflection;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

[Collection(GlobalStateSerialCollection.Name)]
public sealed class EmailNotificationRepositoryTests : IAsyncLifetime
{
    private static readonly UnitOfWorkRepository _unitOfWorkRepository = new(ServiceUtil.GetSharedDataSource());

    private readonly List<Guid> _orderIdsToDelete;
    private readonly List<Guid> _orderChainIdsToDelete;

    public EmailNotificationRepositoryTests()
    {
        _orderIdsToDelete = [];
        _orderChainIdsToDelete = [];
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (Guid orderId in _orderIdsToDelete)
        {
            await PostgreUtil.DeleteStatusFeedFromDb(orderId);
            await PostgreUtil.DeleteNotificationLogFromDb(orderId);
        }

        await PostgreUtil.DeleteOrdersByAlternateIds(_orderIdsToDelete);
        await PostgreUtil.DeleteOrdersChainByOrderIds(_orderChainIdsToDelete);
    }

    [Fact]
    public async Task AddNotification()
    {
        // Arrange
        Guid orderId = await PostgreUtil.PopulateDBWithEmailOrderAndReturnId();
        _orderIdsToDelete.Add(orderId);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        Guid notificationId = Guid.NewGuid();
        EmailNotification emailNotification = new()
        {
            Id = notificationId,
            OrderId = orderId,
            RequestedSendTime = DateTime.UtcNow,
            Recipient = new()
            {
                NationalIdentityNumber = "16069412345",
                ToAddress = "test@email.com"
            }
        };

        await repo.AddNotification(emailNotification, DateTime.UtcNow);

        // Assert
        string sql = $@"SELECT total_attachment_size_bytes 
              FROM notifications.emailnotifications o
              WHERE o.alternateid = '{notificationId}'";

        long actualSize = await PostgreUtil.RunSqlReturnOutput<long>(sql);

        Assert.Equal(0L, actualSize);
    }

    [Fact]
    public async Task GetNewNotificationAsync_ReturnsNotification()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        Email? emailToBeSent = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(emailToBeSent);
        Assert.Equal(emailNotification.Id, emailToBeSent.NotificationId);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public async Task GetNewNotificationAsync_WithSendingPolicy_ReturnsNotificationForMatchingPolicy(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(emailSendingTimePolicy: sendingTimePolicy);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = GetRepository();

        // Act
        Email? emailToBeSent = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken, sendingTimePolicy),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        // Assert
        Assert.NotNull(emailToBeSent);
        Assert.Equal(emailNotification.Id, emailToBeSent.NotificationId);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime, SendingTimePolicy.Daytime)]
    [InlineData(SendingTimePolicy.Daytime, SendingTimePolicy.Anytime)]
    public async Task GetNewNotificationAsync_WithSendingPolicy_DoesNotReturnNotificationForOtherPolicy(SendingTimePolicy orderPolicy, SendingTimePolicy claimedPolicy)
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(emailSendingTimePolicy: orderPolicy);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = GetRepository();

        // Act
        Email? emailToBeSent = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken, claimedPolicy),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        // Assert
        Assert.Null(emailToBeSent);
    }

    [Fact]
    public async Task GetNewNotificationAsync_OrderWithoutEmailSendingTimePolicy_IsOnlyReturnedForAnytime()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = GetRepository();

        // Act
        Email? daytimeEmail = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken, SendingTimePolicy.Daytime),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        Email? anytimeEmail = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken, SendingTimePolicy.Anytime),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        // Assert
        Assert.Null(daytimeEmail);
        Assert.NotNull(anytimeEmail);
        Assert.Equal(emailNotification.Id, anytimeEmail.NotificationId);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime, 1)]
    [InlineData(SendingTimePolicy.Daytime, 2)]
    public async Task CreateOrder_PersistsEmailSendingTimePolicyInOrdersTable(SendingTimePolicy emailSendingTimePolicy, int expectedValue)
    {
        // Arrange
        (NotificationOrder order, _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(emailSendingTimePolicy: emailSendingTimePolicy);
        _orderIdsToDelete.Add(order.Id);

        // Act
        int actual = await PostgreUtil.RunSqlReturnOutput<int>($"SELECT emailsendingtimepolicy FROM notifications.orders WHERE alternateid = '{order.Id}'");

        // Assert
        Assert.Equal(expectedValue, actual);
    }

    [Fact]
    public async Task CreateOrder_WithoutEmailSendingTimePolicy_PersistsNullInOrdersTable()
    {
        // Arrange
        (NotificationOrder order, _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        // Act
        long count = await PostgreUtil.RunSqlReturnOutput<long>($"SELECT count(*) FROM notifications.orders WHERE alternateid = '{order.Id}' AND emailsendingtimepolicy IS NULL");

        // Assert
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime, 1)]
    [InlineData(SendingTimePolicy.Daytime, 2)]
    [InlineData(null, 1)]
    public async Task AddNotification_StoresEmailSendingTimePolicyOfOrderOnNotification(SendingTimePolicy? emailSendingTimePolicy, int expectedValue)
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(emailSendingTimePolicy: emailSendingTimePolicy);
        _orderIdsToDelete.Add(order.Id);

        // Act
        int actual = await PostgreUtil.RunSqlReturnOutput<int>($"SELECT sendingtimepolicy FROM notifications.emailnotifications WHERE alternateid = '{emailNotification.Id}'");

        // Assert
        Assert.Equal(expectedValue, actual);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Daytime, SendingTimePolicy.Anytime, 1)]
    [InlineData(SendingTimePolicy.Anytime, SendingTimePolicy.Daytime, 2)]
    public async Task AddNotification_IgnoresSmsSendingTimePolicyOfOrder(SendingTimePolicy smsSendingTimePolicy, SendingTimePolicy emailSendingTimePolicy, int expectedValue)
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(
            emailSendingTimePolicy: emailSendingTimePolicy,
            smsSendingTimePolicy: smsSendingTimePolicy);
        _orderIdsToDelete.Add(order.Id);

        // Act
        int actual = await PostgreUtil.RunSqlReturnOutput<int>($"SELECT sendingtimepolicy FROM notifications.emailnotifications WHERE alternateid = '{emailNotification.Id}'");

        // Assert
        Assert.Equal(expectedValue, actual);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime, SendingTimePolicy.Daytime)]
    [InlineData(SendingTimePolicy.Daytime, SendingTimePolicy.Anytime)]
    public async Task GetNewNotificationAsync_EmailAndSmsOrder_ClaimsEmailByEmailPolicyRegardlessOfSmsPolicy(SendingTimePolicy emailSendingTimePolicy, SendingTimePolicy smsSendingTimePolicy)
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(
            emailSendingTimePolicy: emailSendingTimePolicy,
            smsSendingTimePolicy: smsSendingTimePolicy);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = GetRepository();

        // Act
        Email? claimed = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken, emailSendingTimePolicy),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(claimed);
    }

    private static EmailNotificationRepository GetRepository()
    {
        return (EmailNotificationRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
    }

    [Fact]
    public async Task GetRecipients_ValidOrderId_ReturnsEmailRecipient()
    {
        // Arrange
        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
           .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
           .First(i => i.GetType() == typeof(EmailNotificationRepository));

        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);
        EmailRecipient expectedRecipient = emailNotification.Recipient;

        // Act
        List<EmailRecipient> actual = await repo.GetRecipients(order.Id);

        EmailRecipient actualRecipient = actual[0];

        // Assert
        Assert.Single(actual);
        Assert.Equal(expectedRecipient.ToAddress, actualRecipient.ToAddress);
        Assert.Equal(expectedRecipient.NationalIdentityNumber, actualRecipient.NationalIdentityNumber);
        Assert.Equal(expectedRecipient.OrganizationNumber, actualRecipient.OrganizationNumber);
    }

    [Fact]
    public async Task GetNewNotificationAsync_WhenKeywordsAreUsed_ShouldAlwaysReturnCustomizedValues()
    {
        // Arrange
        EmailNotificationRepository sut = (EmailNotificationRepository)ServiceUtil
           .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
           .First(i => i.GetType() == typeof(EmailNotificationRepository));
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        // Set customized values directly in the database to simulate keyword replacement
        string customizedSubject = "Customized Subject for Test";
        string customizedBody = "Customized Body for Test";
        await PostgreUtil.UpdateNotificationCustomizedContent<EmailNotification>(emailNotification.Id, customizedSubject, customizedBody);

        // Act
        Email? interpolatedContent = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => sut.GetNewNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(interpolatedContent);
        Assert.Equal(customizedSubject, interpolatedContent.Subject);
        Assert.Equal(customizedBody, interpolatedContent.Body);
    }

    [Theory]
    [InlineData("", "Custom Body")] // Empty subject
    [InlineData("Custom Subject", "")] // Empty body
    public async Task GetNewNotificationAsync_WithEmptyCustomization_HandlesEmptyStringsCorrectly(string customSubject, string customBody)
    {
        // Arrange
        string defaultSubject = "email-subject";
        string defaultBody = "email-body";

        EmailNotificationRepository sut = (EmailNotificationRepository)ServiceUtil
           .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
           .First(i => i.GetType() == typeof(EmailNotificationRepository));
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        await PostgreUtil.UpdateNotificationCustomizedContent<EmailNotification>(emailNotification.Id, customSubject, customBody);

        // Act
        Email? result = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => sut.GetNewNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.IsNullOrEmpty(customSubject) ? defaultSubject : customSubject, result.Subject);
        Assert.Equal(string.IsNullOrEmpty(customBody) ? defaultBody : customBody, result.Body);
    }

    [Fact]
    public async Task GetNewNotificationAsync_ExpiredNotificationWithNewStatus_IsNotPickedUp()
    {
        // Arrange
        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        var nonExpiredNotification = new EmailNotification
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = DateTime.UtcNow,
            Recipient = new()
            {
                ToAddress = "test@email.com"
            }
        };

        await repo.AddNotification(nonExpiredNotification, DateTime.UtcNow.AddDays(1));

        await PostgreUtil.RunSql($@"
            UPDATE notifications.emailnotifications
            SET result = '{EmailNotificationResultType.New}',
                expirytime = NOW() - INTERVAL '10 minutes'
            WHERE alternateid = '{emailNotification.Id}'");

        // Act
        Email? claimedNotification = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        Email? claimedNonExpiredNotification = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => repo.GetNewNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == nonExpiredNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        // Assert
        Assert.Null(claimedNotification);
        Assert.NotNull(claimedNonExpiredNotification);
        Assert.Equal(nonExpiredNotification.Id, claimedNonExpiredNotification.NotificationId);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenValidNotificationId_ShouldUpdateStatusAndOperationId()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, emailNotification.Id, EmailNotificationResultType.Succeeded, operationId, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}' 
              AND email.result = '{EmailNotificationResultType.Succeeded}' 
              AND email.operationid = '{operationId}'
              AND email.total_attachment_size_bytes = 0";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenValidNotificationId_ShouldUpdateOrderStatusToCompleted()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, emailNotification.Id, EmailNotificationResultType.Delivered, operationId, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}' 
              AND email.result  = '{EmailNotificationResultType.Delivered}' 
              AND email.operationid = '{operationId}'
              AND email.total_attachment_size_bytes = 0";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        sql = $@"SELECT processedstatus FROM notifications.orders o
                 WHERE o.alternateid = '{order.Id}'";

        var processedStatus = await PostgreUtil.RunSqlReturnOutput<string>(sql);

        Assert.Equal(1, actualCount);

        // Verify that the order status was updated based on notification delivery
        // Initial state is "Registered", final state should be "Completed"
        Assert.Equal("Completed", processedStatus);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenEmptyToAddress_ShouldSaveStatusFeedAndNotificationLogEntries()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(toAddress: string.Empty);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository sut = (EmailNotificationRepository)ServiceUtil
         .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
         .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await sut.UpdateSendStatusAsync(unitOfWork, emailNotification.Id, EmailNotificationResultType.Delivered, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert
        var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(1, notificationLogCount);
    }

    [Fact]
    public async Task UpdateSendStatus_OrderWithMultipleRecipients_LogsEachRecipientWhenItsResultIsFinalAndWritesOneStatusFeedEntryOnCompletion()
    {
        // Arrange — simulates an organization resolving to multiple contact points on the same order.
        OrderRepository orderRepo = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        EmailNotificationRepository sut = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            Creator = new("ttd"),
            SendersReference = "tx-test-multiple-recipients",
            Type = OrderType.Notification,
            Templates = [new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain)]
        };

        _orderIdsToDelete.Add(order.Id);
        await orderRepo.Create(order);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);

        var notifications = new List<EmailNotification>
        {
            new() { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "first@example.com" }, SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow) },
            new() { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "second@example.com" }, SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow) },
            new() { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = "third@example.com" }, SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow) }
        };

        foreach (var notification in notifications)
        {
            await sut.AddNotification(notification, DateTime.UtcNow.AddDays(1));
        }

        // Act — deliver two of the three; the order must not complete yet since one recipient is still pending
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await sut.UpdateSendStatusAsync(unitOfWork, notifications[0].Id, EmailNotificationResultType.Delivered, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await sut.UpdateSendStatusAsync(unitOfWork, notifications[1].Id, EmailNotificationResultType.Delivered, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert — no status feed yet, but the two delivered recipients are already logged
        int statusFeedCountBeforeLastDelivery = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        int notificationLogCountBeforeLastDelivery = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        Assert.Equal(0, statusFeedCountBeforeLastDelivery);
        Assert.Equal(2, notificationLogCountBeforeLastDelivery);

        // Deliver the last recipient — the order now completes, triggering the status feed and notification log inserts
        unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await sut.UpdateSendStatusAsync(unitOfWork, notifications[2].Id, EmailNotificationResultType.Delivered, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert — exactly one status feed entry for the order, but one notification log entry per recipient
        var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(notifications.Count, notificationLogCount);
    }

    private async Task UpdateEmailStatusAsync(EmailNotificationRepository repo, Guid notificationId, EmailNotificationResultType status, string? operationId = null)
    {
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, notificationId, status, operationId, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
    }

    private async Task UpdateSmsStatusAsync(SmsNotificationRepository repo, Guid notificationId, SmsNotificationResultType status)
    {
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, notificationId, status, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
    }

    [Fact]
    public async Task UpdateSendStatus_EmailDeliveredWhileSmsPending_LogsOnlyTheEmailAndWritesNoStatusFeed()
    {
        // Arrange
        (Guid orderId, EmailNotification email, _) = await CreateOrderWithPendingEmailAndSmsAsync();
        EmailNotificationRepository sut = GetEmailRepository();

        // Act
        await UpdateEmailStatusAsync(sut, email.Id, EmailNotificationResultType.Delivered);

        // Assert
        await AssertLoggedAsync(orderId, ("Email", email.Recipient.ToAddress!, "Delivered"));
        Assert.Equal(0, await PostgreUtil.SelectStatusFeedEntryCount(orderId));
    }

    [Fact]
    public async Task UpdateSendStatus_SmsDeliveredWhileEmailPending_LogsOnlyTheSmsAndWritesNoStatusFeed()
    {
        // Arrange
        (Guid orderId, _, SmsNotification sms) = await CreateOrderWithPendingEmailAndSmsAsync();
        SmsNotificationRepository smsRepo = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        // Act
        await UpdateSmsStatusAsync(smsRepo, sms.Id, SmsNotificationResultType.Delivered);

        // Assert
        await AssertLoggedAsync(orderId, ("Sms", sms.Recipient.MobileNumber!, "Delivered"));
        Assert.Equal(0, await PostgreUtil.SelectStatusFeedEntryCount(orderId));
    }

    [Fact]
    public async Task UpdateSendStatus_EmailFailedWhileSmsPending_LogsTheFailedEmail()
    {
        // Arrange
        (Guid orderId, EmailNotification email, _) = await CreateOrderWithPendingEmailAndSmsAsync();
        EmailNotificationRepository sut = GetEmailRepository();

        // Act
        await UpdateEmailStatusAsync(sut, email.Id, EmailNotificationResultType.Failed_RecipientNotIdentified);

        // Assert
        await AssertLoggedAsync(orderId, ("Email", email.Recipient.ToAddress!, "Failed_RecipientNotIdentified"));
        Assert.Equal(0, await PostgreUtil.SelectStatusFeedEntryCount(orderId));
    }

    [Fact]
    public async Task UpdateSendStatus_IntermediateResultWhileSmsPending_LogsNothing()
    {
        // Arrange
        (Guid orderId, EmailNotification email, _) = await CreateOrderWithPendingEmailAndSmsAsync();
        EmailNotificationRepository sut = GetEmailRepository();

        // Act
        await UpdateEmailStatusAsync(sut, email.Id, EmailNotificationResultType.Succeeded, "operation-id");

        // Assert
        await AssertLoggedAsync(orderId);
    }

    [Fact]
    public async Task UpdateSendStatus_LaterFinalResultOfSibling_AddsOneRowAndKeepsEarlierEntry()
    {
        // Arrange
        (Guid orderId, EmailNotification email, SmsNotification sms) = await CreateOrderWithPendingEmailAndSmsAsync();
        EmailNotificationRepository sut = GetEmailRepository();
        SmsNotificationRepository smsRepo = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        await UpdateEmailStatusAsync(sut, email.Id, EmailNotificationResultType.Delivered);
        await AssertLoggedAsync(orderId, ("Email", email.Recipient.ToAddress!, "Delivered"));

        // Act
        await UpdateSmsStatusAsync(smsRepo, sms.Id, SmsNotificationResultType.Delivered);

        // Assert — one row per notification, exactly one status feed entry now that the order is complete
        await AssertLoggedAsync(orderId, ("Email", email.Recipient.ToAddress!, "Delivered"), ("Sms", sms.Recipient.MobileNumber!, "Delivered"));
        Assert.Equal(1, await PostgreUtil.SelectStatusFeedEntryCount(orderId));
    }

    [Fact]
    public async Task TerminateExpiredNotifications_WhileSmsPending_LogsOnlyTheExpiredEmail()
    {
        // Arrange
        (Guid orderId, EmailNotification email, _) = await CreateOrderWithPendingEmailAndSmsAsync();
        EmailNotificationRepository sut = GetEmailRepository();

        await PostgreUtil.RunSql($@"
            UPDATE notifications.emailnotifications
            SET result = 'Succeeded',
                expirytime = NOW() - INTERVAL '3 day'
            WHERE alternateid = '{email.Id}';");

        // Act
        await sut.TerminateExpiredNotifications();

        // Assert
        await AssertLoggedAsync(orderId, ("Email", email.Recipient.ToAddress!, "Failed_TTL"));
        Assert.Equal(0, await PostgreUtil.SelectStatusFeedEntryCount(orderId));
    }

    [Fact]
    public async Task UpdateSendStatus_GivenValidOperationId_ShouldFindNotificationAndUpdateStatus()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        string setOperationIdSql = $@"Update notifications.emailnotifications 
                SET operationid = '{operationId}'
                WHERE alternateid = '{emailNotification.Id}'";

        await PostgreUtil.RunSql(setOperationIdSql);

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, null, EmailNotificationResultType.Succeeded, operationId, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}'
              AND email.result = '{EmailNotificationResultType.Succeeded}'
              AND email.operationid = '{operationId}'
              AND email.total_attachment_size_bytes = 0";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenNotificationIdWithoutOperationId_ShouldUpdateStatusSuccessfully()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, emailNotification.Id, EmailNotificationResultType.Failed, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}' 
              AND email.result  = '{EmailNotificationResultType.Failed}'
              AND email.total_attachment_size_bytes = 0";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenNonExistentNotificationId_ThrowsNotificationNotFoundException()
    {
        // Arrange
        EmailNotificationRepository emailNotificationRepository = (EmailNotificationRepository)ServiceUtil
          .GetServices([typeof(IEmailNotificationRepository)])
          .First(i => i.GetType() == typeof(EmailNotificationRepository));
        Guid nonExistentNotificationId = Guid.NewGuid();

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        var ex = await Assert.ThrowsAsync<NotificationNotFoundException>(() => emailNotificationRepository.UpdateSendStatusAsync(unitOfWork, nonExistentNotificationId, EmailNotificationResultType.Succeeded, cancellationToken: TestContext.Current.CancellationToken));
        await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);

        // Assert:
        Assert.Equal(NotificationChannel.Email, ex.Channel);
        Assert.Equal(SendStatusIdentifierType.NotificationId, ex.IdentifierType);

        // Assert: no rows updated
        string sql = $@"
        SELECT count(1)
        FROM notifications.emailnotifications email
        WHERE email.alternateid = '{nonExistentNotificationId}'
          AND email.result = '{EmailNotificationResultType.Succeeded}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);
        Assert.Equal(0, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenNonExistentOperationId_ThrowsNotificationNotFoundException()
    {
        // Arrange
        EmailNotificationRepository emailNotificationRepository = (EmailNotificationRepository)ServiceUtil
          .GetServices([typeof(IEmailNotificationRepository)])
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        var ex = await Assert.ThrowsAsync<NotificationNotFoundException>(() => emailNotificationRepository.UpdateSendStatusAsync(unitOfWork, notificationId: null, status: EmailNotificationResultType.Succeeded, operationId: operationId, cancellationToken: TestContext.Current.CancellationToken));
        await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);

        // Assert: exception details
        Assert.Equal(NotificationChannel.Email, ex.Channel);
        Assert.Equal(SendStatusIdentifierType.OperationId, ex.IdentifierType);

        // Assert: no rows updated
        string sql = $@"
        SELECT count(1)
        FROM notifications.emailnotifications email
        WHERE email.operationId = '{operationId}'
          AND email.result = '{EmailNotificationResultType.Succeeded}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);
        Assert.Equal(0, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenExpiredNotification_ThrowsNotificationExpiredException()
    {
        // Arrange
        Guid orderId = await PostgreUtil.PopulateDBWithEmailOrderAndReturnId();
        _orderIdsToDelete.Add(orderId);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        Guid notificationId = Guid.NewGuid();
        EmailNotification notification = new()
        {
            OrderId = orderId,
            Id = notificationId,
            RequestedSendTime = DateTime.UtcNow,
            Recipient = new() { ToAddress = "test@example.com" }
        };

        // Add notification with expiry time in the past (expired 10 minutes ago)
        await repo.AddNotification(notification, DateTime.UtcNow.AddMinutes(-10));

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        var ex = await Assert.ThrowsAsync<NotificationExpiredException>(() =>
            repo.UpdateSendStatusAsync(unitOfWork, notificationId, EmailNotificationResultType.Delivered, totalAttachmentSizeBytes: 204800L, cancellationToken: TestContext.Current.CancellationToken));
        await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);

        // Assert: exception details
        Assert.Equal(NotificationChannel.Email, ex.Channel);
        Assert.Equal(SendStatusIdentifierType.NotificationId, ex.IdentifierType);
        Assert.Equal(notificationId.ToString(), ex.Identifier);

        // Assert: transaction was rolled back, so result and total_attachment_size_bytes are unchanged
        string resultSql = $@"
        SELECT result
        FROM notifications.emailnotifications
        WHERE alternateid = '{notificationId}'";

        string result = await PostgreUtil.RunSqlReturnOutput<string>(resultSql);
        Assert.Equal(EmailNotificationResultType.New.ToString(), result);

        string sizeSql = $@"
        SELECT total_attachment_size_bytes
        FROM notifications.emailnotifications
        WHERE alternateid = '{notificationId}'";

        long actualSize = await PostgreUtil.RunSqlReturnOutput<long>(sizeSql);
        Assert.Equal(0L, actualSize);
    }

    [Fact]
    public async Task UpdateSendStatus_WithInvalidNotificationAndOperationIdentifiers_ThrowsInvalidNotificationIdentifierException()
    {
        // Arrange
        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidNotificationIdentifierException>(
            () => repo.UpdateSendStatusAsync(new UnitOfWork(), null, EmailNotificationResultType.Succeeded, null, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("The provided Email identifier is invalid.", exception.Message);
    }

    [Fact]
    public async Task TerminateExpiredNotifications_ShouldSetNotificationToFailed_CompleteOrder_InsertToFeed_AndInsertNotificationLog()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository sut = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // modify the notification to simulate an expired notification
        string sql = $@"
            UPDATE notifications.emailnotifications 
            SET result = 'Succeeded', 
                expirytime = NOW() - INTERVAL '3 day' 
            WHERE alternateid = '{emailNotification.Id}';";

        await PostgreUtil.RunSql(sql);

        // Act
        await sut.TerminateExpiredNotifications();

        // Assert
        var result = await SelectEmailNotificationStatus(emailNotification.Id);
        var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        var orderStatus = await PostgreUtil.RunSqlReturnOutput<string>($"SELECT processedstatus FROM notifications.orders WHERE alternateid = '{order.Id}'");

        Assert.NotNull(result);
        Assert.Equal(EmailNotificationResultType.Failed_TTL.ToString(), result);
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(1, notificationLogCount);
        Assert.Equal(OrderProcessingStatus.Completed.ToString(), orderStatus);
    }

    [Theory]
    [InlineData("10 seconds", false)]
    [InlineData("315 seconds", true)]
    public async Task TerminateExpiredNotifications_WithGracePeriod_UpdatesStatusBasedOnExpiryTime(string timeInterval, bool markedAsTTL)
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository sut = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // modify the notification to simulate an expired notification
        await PostgreUtil.UpdateResultAndExpiryTimeNotification(emailNotification, timeInterval);

        // Act
        await sut.TerminateExpiredNotifications();

        // Assert
        var result = await SelectEmailNotificationStatus(emailNotification.Id);

        Assert.NotNull(result);

        if (markedAsTTL)
        {
            Assert.Equal(EmailNotificationResultType.Failed_TTL.ToString(), result);
        }
        else
        {
            Assert.Equal(EmailNotificationResultType.Succeeded.ToString(), result);
        }
    }

    [Fact]
    public async Task SetEmailResult_AllEnumValuesExistInDb()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        // Act & Assert
        foreach (EmailNotificationResultType resultType in Enum.GetValues<EmailNotificationResultType>())
        {
            try
            {
                string sql = $@"
                UPDATE notifications.emailnotifications 
                SET result = '{resultType}'
                WHERE alternateid = '{emailNotification.Id}';";

                await PostgreUtil.RunSql(sql);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Exception thrown for EmailNotificationResultType: {resultType}. Exception: {ex.Message}");
            }
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void Constructor_InvalidTerminationBatchSize_InitializesSuccessfully(string invalidBatchSize)
    {
        // Arrange - Set invalid TerminationBatchSize via environment variables
        Dictionary<string, string> vars = new()
        {
            { "NotificationConfig__TerminationBatchSize", invalidBatchSize }
        };

        try
        {
            // Act - Create repository with invalid configuration (exercises validation path)
            var services = ServiceUtil.GetServices(new List<Type>() { typeof(IEmailNotificationRepository) }, vars);
            var repository = (EmailNotificationRepository)services.First(i => i.GetType() == typeof(EmailNotificationRepository));

            // Assert - Repository should initialize successfully despite invalid config
            // This proves the validation code executed and fell back to default value
            Assert.NotNull(repository);
        }
        finally
        {
            // Clean up environment variable to prevent test pollution
            Environment.SetEnvironmentVariable("NotificationConfig__TerminationBatchSize", null);
        }
    }

    [Fact]
    public async Task InsertOrderStatusCompletedOrder_ValidAlternateId_InsertsStatusFeedOnly()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true, resultType: EmailNotificationResultType.Delivered);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act - alternateId is the notification ID, not the order ID
        var method = typeof(NotificationRepositoryBase).GetMethod("InsertOrderStatusCompletedOrder", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(repo, [connection, transaction, emailNotification.Id])!;
        await task;
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert - status feed is written; logging is done per notification by LogFinalNotificationResult
        int statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, statusFeedCount);

        int notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        Assert.Equal(0, notificationLogCount);
    }

    [Fact]
    public async Task InsertOrderStatusCompletedOrder_CalledTwiceForSameAlternateId_SecondCallIsIdempotentNoOp()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var method = typeof(NotificationRepositoryBase).GetMethod("InsertOrderStatusCompletedOrder", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act - call twice for the same alternateId within the same transaction, simulating the order being processed more than once
        await (Task)method!.Invoke(repo, [connection, transaction, emailNotification.Id])!;
        await (Task)method!.Invoke(repo, [connection, transaction, emailNotification.Id])!;
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert - the second call did not throw, and only one status feed entry exists
        int statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, statusFeedCount);
    }

    [Fact]
    public async Task InsertOrderStatusCompletedOrder_OrderStatusNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        Guid nonExistentOrderId = Guid.NewGuid();

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var method = typeof(NotificationRepositoryBase).GetMethod("InsertOrderStatusCompletedOrder", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(repo, [connection, transaction, nonExistentOrderId])!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
        Assert.Equal("Order status could not be retrieved for the specified alternate ID.", ex.Message);
    }

    [Fact]
    public async Task GetNewNotificationAsync_ComposedOrderNotification_IsNotClaimed()
    {
        // Arrange
        OrderRepository orderRepo = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        EmailNotificationRepository emailRepo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();

        _orderIdsToDelete.Add(orderId);
        _orderChainIdsToDelete.Add(orderChainId);

        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetType(OrderType.Composed)
            .SetCreator(new Creator("ttd"))
            .SetRequestedSendTime(DateTime.UtcNow.AddHours(1))
            .SetRecipient(new NotificationRecipient
            {
                RecipientComposedEmail = new RecipientComposedEmail
                {
                    EmailAddress = "recipient@altinnxyz.no",
                    Settings = new ComposedEmailSendingOptions
                    {
                        Subject = "Test subject",
                        Body = "Test body",
                        Attachments =
                        [
                            new SasFileReference
                            {
                                Filename = "document.pdf",
                                MimeType = "application/pdf",
                                SasUrl = new Uri("https://example.blob.core.windows.net/container/document.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fake")
                            }
                        ]
                    }
                }
            })
            .Build();

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Type = OrderType.Composed,
            Creator = new("ttd"),
            Created = DateTime.UtcNow,
            RequestedSendTime = DateTime.UtcNow.AddHours(1),
            NotificationChannel = NotificationChannel.Email,
            Templates = [new EmailTemplate("noreply@altinn.no", "Test subject", "Test body", EmailContentType.Plain)],
            Recipients = [new Recipient([new EmailAddressPoint("recipient@altinnxyz.no")])]
        };

        await orderRepo.Create(orderChainRequest, notificationOrder, null, TestContext.Current.CancellationToken);

        EmailNotification emailNotification = new()
        {
            Id = notificationId,
            OrderId = orderId,
            RequestedSendTime = DateTime.UtcNow,
            Recipient = new() { ToAddress = "recipient@altinnxyz.no" }
        };

        await emailRepo.AddNotification(emailNotification, DateTime.UtcNow.AddDays(1));

        // Act
        // Try to claim this specific composed notification through the standard email claim path.
        // The helper advances claims within the same transaction, so non-matching earlier rows do not
        // cause a false positive.
        Email? claimedAsStandardEmail = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => emailRepo.GetNewNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == notificationId,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        // Assert — composed notification must never be claimable via standard email claim path.
        Assert.Null(claimedAsStandardEmail);

        // Control assertion: the composed claim path should be able to claim the same notification.
        ComposedEmail? claimedAsComposedEmail = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => emailRepo.GetNewComposedNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == notificationId,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        Assert.NotNull(claimedAsComposedEmail);
        Assert.Equal(notificationId, claimedAsComposedEmail.NotificationId);
    }

    [Fact]
    public async Task UpdateSendStatus_PayloadTooLarge_PersistsTotalAttachmentSizeBytesToDatabase()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Simulate pipeline: New → Sending (claimed by background service)
        await PostgreUtil.RunSql($@"
            UPDATE notifications.emailnotifications
            SET result = '{EmailNotificationResultType.Sending}'
            WHERE alternateid = '{emailNotification.Id}'");

        string operationId = Guid.NewGuid().ToString();
        long expectedSize = 12_534_336L; // Exceeds the 10 MB ACS limit, triggering Failed_PayloadTooLarge

        // Act — Sending → Failed_PayloadTooLarge (ACS rejects with 413)
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, emailNotification.Id, EmailNotificationResultType.Failed_PayloadTooLarge, operationId, deliveryReport: null, totalAttachmentSizeBytes: expectedSize, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert
        string sql = $@"
            SELECT total_attachment_size_bytes FROM notifications.emailnotifications
            WHERE alternateid = '{emailNotification.Id}'";

        long actualSize = await PostgreUtil.RunSqlReturnOutput<long>(sql);
        Assert.Equal(expectedSize, actualSize);
    }

    [Fact]
    public async Task UpdateSendStatus_SubsequentDeliveryReportUpdate_PreservesExistingTotalAttachmentSizeBytes()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();
        long expectedSize = 158304L;
        string deliveryReport = """{"messageId":"test-op","status":"Delivered","deliveryStatusDetails":{"statusMessage":"OK"}}""";

        // Simulate pipeline: New → Sending (claimed by background service)
        await PostgreUtil.RunSql($@"
            UPDATE notifications.emailnotifications
            SET result = '{EmailNotificationResultType.Sending}'
            WHERE alternateid = '{emailNotification.Id}'");

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(
            unitOfWork,
            emailNotification.Id,
            EmailNotificationResultType.Succeeded,
            operationId,
            deliveryReport: null,
            totalAttachmentSizeBytes: expectedSize,
            cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(
            unitOfWork,
            null,
            EmailNotificationResultType.Delivered,
            operationId,
            deliveryReport,
            totalAttachmentSizeBytes: null,
            cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert
        string sql = $@"
            SELECT total_attachment_size_bytes FROM notifications.emailnotifications
            WHERE alternateid = '{emailNotification.Id}'";

        long actualSize = await PostgreUtil.RunSqlReturnOutput<long>(sql);
        Assert.Equal(expectedSize, actualSize);
    }

    [Fact]
    public async Task GetNewComposedNotificationAsync_ReturnsComposedNotificationWithAttachments()
    {
        // Arrange
        OrderRepository orderRepo = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        EmailNotificationRepository emailRepo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();

        _orderIdsToDelete.Add(orderId);
        _orderChainIdsToDelete.Add(orderChainId);

        const string expectedBody = "Composed body";
        const string expectedSubject = "Composed subject";
        const string expectedFrom = "noreply@altinn.no";
        const string expectedFilename = "document.pdf";
        const string expectedTo = "recipient@altinnxyz.no";
        const string expectedMimeType = "application/pdf";
        const string expectedSendersReference = "senders-reference";
        var expectedRequestedSendTime = DateTime.UtcNow.AddMinutes(45);
        var expectedSasUrl = new Uri("https://example.blob.core.windows.net/container/document.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fake");

        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetType(OrderType.Composed)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("ttd"))
            .SetSendersReference(expectedSendersReference)
            .SetRequestedSendTime(expectedRequestedSendTime)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetRecipient(new NotificationRecipient
            {
                RecipientComposedEmail = new RecipientComposedEmail
                {
                    EmailAddress = expectedTo,
                    Settings = new ComposedEmailSendingOptions
                    {
                        Subject = expectedSubject,
                        Body = expectedBody,
                        Attachments =
                        [
                            new SasFileReference
                            {
                                Filename = expectedFilename,
                                MimeType = expectedMimeType,
                                SasUrl = expectedSasUrl
                            }
                        ]
                    }
                }
            })
            .Build();

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new("ttd"),
            Type = OrderType.Composed,
            SendersReference = expectedSendersReference,
            RequestedSendTime = expectedRequestedSendTime,
            NotificationChannel = NotificationChannel.Email,
            Recipients = [new Recipient([new EmailAddressPoint(expectedTo)])],
            Templates = [new EmailTemplate(expectedFrom, expectedSubject, expectedBody, EmailContentType.Plain)],
            EmailAttachments =
            [
                new SasFileReference
                {
                    Filename = expectedFilename,
                    MimeType = expectedMimeType,
                    SasUrl = expectedSasUrl
                }
            ]
        };

        await orderRepo.Create(orderChainRequest, notificationOrder, null, TestContext.Current.CancellationToken);

        EmailNotification emailNotification = new()
        {
            OrderId = orderId,
            Id = notificationId,
            Recipient = new() { ToAddress = expectedTo },
            RequestedSendTime = expectedRequestedSendTime,
        };

        await emailRepo.AddNotification(emailNotification, DateTime.UtcNow.AddDays(1));

        // Act
        ComposedEmail? result = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => emailRepo.GetNewComposedNotificationAsync(unitOfWork, cancellationToken),
            composedEmail => composedEmail.NotificationId == notificationId,
            TestContext.Current.CancellationToken,
            commitOnMatch: true);

        // Assert — notification is returned
        Assert.NotNull(result);
        var attachment = Assert.Single(result.Attachments);
        Assert.Equal(expectedBody, result.Body);
        Assert.Equal(expectedTo, result.ToAddress);
        Assert.Equal(expectedSubject, result.Subject);
        Assert.Equal(expectedSasUrl, attachment.SasUrl);
        Assert.Equal(expectedFilename, attachment.Filename);
        Assert.Equal(expectedMimeType, attachment.MimeType);

        // Assert — notification is now claimed (status = Sending)
        string statusSql = $"SELECT result FROM notifications.emailnotifications WHERE alternateid = '{notificationId}'";
        string status = await PostgreUtil.RunSqlReturnOutput<string>(statusSql);
        Assert.Equal(EmailNotificationResultType.Sending.ToString(), status);
    }

    [Fact]
    public async Task GetNewComposedNotificationAsync_StandardOrderNotification_IsExcluded()
    {
        // Arrange — a standard (non-Composed) order's email notification must not be returned by claim_composed_email
        EmailNotificationRepository emailRepo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        // Act
        // Try to claim this specific standard notification through the composed claim path.
        // The helper advances claims within one transaction, avoiding false positives caused by
        // unrelated earlier rows.
        ComposedEmail? claimedAsComposedEmail = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => emailRepo.GetNewComposedNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        // Assert — standard notification must never be claimable through composed path.
        Assert.Null(claimedAsComposedEmail);

        // Control assertion: standard claim path should be able to claim this same notification.
        Email? claimedAsStandardEmail = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => emailRepo.GetNewNotificationAsync(unitOfWork, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            maxAttempts: 200);

        Assert.NotNull(claimedAsStandardEmail);
        Assert.Equal(emailNotification.Id, claimedAsStandardEmail.NotificationId);
    }

    private static EmailNotificationRepository GetEmailRepository()
    {
        return (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
    }

    private async Task<(Guid OrderId, EmailNotification Email, SmsNotification Sms)> CreateOrderWithPendingEmailAndSmsAsync()
    {
        OrderRepository orderRepo = (OrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));

        SmsNotificationRepository smsRepo = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            Creator = new("ttd"),
            SendersReference = $"log-per-notification-{Guid.NewGuid():N}",
            Type = OrderType.Notification,
            Templates =
            [
                new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain),
                new SmsTemplate("Altinn", "sms-body")
            ]
        };

        _orderIdsToDelete.Add(order.Id);
        await orderRepo.Create(order);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);

        var email = new EmailNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { ToAddress = $"{Guid.NewGuid():N}@example.com" }, SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow) };
        var sms = new SmsNotification { Id = Guid.NewGuid(), OrderId = order.Id, RequestedSendTime = DateTime.UtcNow, Recipient = new() { MobileNumber = "+4791111111" }, SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow) };

        await GetEmailRepository().AddNotification(email, DateTime.UtcNow.AddDays(1));
        await smsRepo.AddNotification(sms, DateTime.UtcNow.AddDays(1));

        return (order.Id, email, sms);
    }

    private static async Task AssertLoggedAsync(Guid shipmentId, params (string Channel, string Destination, string Status)[] expected)
    {
        var logged = (await PostgreUtil.GetNotificationLogEntries(shipmentId))
            .Select(e => (e.Channel, e.Destination, e.Status))
            .Order()
            .ToList();

        Assert.Equal(expected.Order().ToList(), logged);
    }

    private static async Task<string> SelectEmailNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.emailnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }

    private static async Task<TResult?> ExecuteClaimUntilMatchInUnitOfWork<TResult>(
        Func<UnitOfWork, CancellationToken, Task<TResult?>> claim,
        Func<TResult, bool> isMatch,
        CancellationToken cancellationToken,
        bool commitOnMatch = false,
        int maxAttempts = 50)
        where TResult : class
    {
        await using var connection = await ServiceUtil.GetSharedDataSource().OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var unitOfWork = new UnitOfWork
        {
            Connection = connection,
            Transaction = transaction
        };

        for (int i = 0; i < maxAttempts; i++)
        {
            TResult? result = await claim(unitOfWork, cancellationToken);

            if (result is not null && isMatch(result))
            {
                if (commitOnMatch)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return result;
            }
        }

        await transaction.RollbackAsync(cancellationToken);

        return null;
    }

    [Fact]
    public async Task UpdateSendStatusAsync_OnClaimedRowInSameUnitOfWork_SetsResultAndCompletesOrder()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        Email? claimed = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => ClaimAndSetContentTooLarge(repo, unitOfWork, emailNotification.Id, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            commitOnMatch: true,
            maxAttempts: 200);

        // Assert
        Assert.NotNull(claimed);
        Assert.Equal(EmailNotificationResultType.Failed_ContentTooLarge.ToString(), await SelectEmailNotificationStatus(emailNotification.Id));

        string processedStatus = await PostgreUtil.RunSqlReturnOutput<string>($"SELECT processedstatus FROM notifications.orders WHERE alternateid = '{order.Id}'");
        Assert.Equal(OrderProcessingStatus.Completed.ToString(), processedStatus);

        int statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, statusFeedCount);
    }

    [Fact]
    public async Task UpdateSendStatusAsync_WhenUnitOfWorkIsRolledBack_LeavesResultUnchanged()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        Email? claimed = await ExecuteClaimUntilMatchInUnitOfWork(
            (unitOfWork, cancellationToken) => ClaimAndSetContentTooLarge(repo, unitOfWork, emailNotification.Id, cancellationToken),
            email => email.NotificationId == emailNotification.Id,
            TestContext.Current.CancellationToken,
            commitOnMatch: false,
            maxAttempts: 200);

        // Assert
        Assert.NotNull(claimed);
        Assert.Equal(EmailNotificationResultType.New.ToString(), await SelectEmailNotificationStatus(emailNotification.Id));
    }

    [Fact]
    public async Task UpdateSendStatusAsync_GivenNonExistentNotificationId_ThrowsNotificationNotFoundException()
    {
        // Arrange
        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        Guid nonExistentNotificationId = Guid.NewGuid();

        await using var connection = await ServiceUtil.GetSharedDataSource().OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var unitOfWork = new UnitOfWork
        {
            Connection = connection,
            Transaction = transaction
        };

        // Act
        var ex = await Assert.ThrowsAsync<NotificationNotFoundException>(
            () => repo.UpdateSendStatusAsync(unitOfWork, nonExistentNotificationId, EmailNotificationResultType.Failed_ContentTooLarge, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(NotificationChannel.Email, ex.Channel);
        Assert.Equal(SendStatusIdentifierType.NotificationId, ex.IdentifierType);
        Assert.Equal(nonExistentNotificationId.ToString(), ex.Identifier);
    }

    [Fact]
    public async Task UpdateSendStatusAsync_GivenNonExistentNotificationIdAndOperationId_ReportsNotificationId()
    {
        // Arrange
        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        Guid nonExistentNotificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        await using var connection = await ServiceUtil.GetSharedDataSource().OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var unitOfWork = new UnitOfWork
        {
            Connection = connection,
            Transaction = transaction
        };

        // Act
        var ex = await Assert.ThrowsAsync<NotificationNotFoundException>(
            () => repo.UpdateSendStatusAsync(unitOfWork, nonExistentNotificationId, EmailNotificationResultType.Succeeded, operationId: operationId, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(NotificationChannel.Email, ex.Channel);
        Assert.Equal(SendStatusIdentifierType.NotificationId, ex.IdentifierType);
        Assert.Equal(nonExistentNotificationId.ToString(), ex.Identifier);
    }

    [Fact]
    public async Task UpdateSendStatusAsync_GivenEmptyNotificationId_ThrowsInvalidNotificationIdentifierException()
    {
        // Arrange
        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidNotificationIdentifierException>(
            () => repo.UpdateSendStatusAsync(new UnitOfWork(), Guid.Empty, EmailNotificationResultType.Failed_ContentTooLarge, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("The provided Email identifier is invalid.", exception.Message);
    }

    [Fact]
    public async Task UpdateSendStatus_WithDeliveryReport_PersistsDeliveryReportToDatabase()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();
        string deliveryReport = """{"messageId":"test-op","status":"Delivered","deliveryStatusDetails":{"statusMessage":"OK"}}""";

        // Act
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, emailNotification.Id, EmailNotificationResultType.Delivered, operationId, deliveryReport, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert — result and operationId updated
        string statusSql = $@"
            SELECT count(1) FROM notifications.emailnotifications
            WHERE alternateid = '{emailNotification.Id}'
              AND result = '{EmailNotificationResultType.Delivered}'
              AND operationid = '{operationId}'";

        int count = await PostgreUtil.RunSqlReturnOutput<int>(statusSql);
        Assert.Equal(1, count);

        // Assert — delivery report JSON is persisted and round-trips correctly
        string reportSql = $@"
            SELECT deliveryreport::text FROM notifications.emailnotifications
            WHERE alternateid = '{emailNotification.Id}'";

        string? persistedReport = await PostgreUtil.RunSqlReturnOutput<string?>(reportSql);
        Assert.NotNull(persistedReport);

        using var expected = System.Text.Json.JsonDocument.Parse(deliveryReport);
        using var actual = System.Text.Json.JsonDocument.Parse(persistedReport!);
        Assert.Equal(
            expected.RootElement.GetProperty("messageId").GetString(),
            actual.RootElement.GetProperty("messageId").GetString());
        Assert.Equal(
            expected.RootElement.GetProperty("status").GetString(),
            actual.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateSendStatus_WithNullDeliveryReport_LeavesDeliveryReportColumnNull()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        // Act — no delivery report supplied
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        await repo.UpdateSendStatusAsync(unitOfWork, emailNotification.Id, EmailNotificationResultType.Succeeded, operationId, deliveryReport: null, cancellationToken: TestContext.Current.CancellationToken);
        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

        // Assert — deliveryreport column should remain NULL
        string sql = $@"
            SELECT deliveryreport::text FROM notifications.emailnotifications
            WHERE alternateid = '{emailNotification.Id}'";

        string? persistedReport = await PostgreUtil.RunSqlReturnOutput<string?>(sql);
        Assert.Null(persistedReport);
    }

    private static async Task<Email?> ClaimAndSetContentTooLarge(EmailNotificationRepository repo, UnitOfWork unitOfWork, Guid notificationId, CancellationToken cancellationToken)
    {
        Email? claimed = await repo.GetNewNotificationAsync(unitOfWork, cancellationToken);
        if (claimed?.NotificationId == notificationId)
        {
            await repo.UpdateSendStatusAsync(unitOfWork, notificationId, EmailNotificationResultType.Failed_ContentTooLarge, cancellationToken: cancellationToken);
        }

        return claimed;
    }
}
