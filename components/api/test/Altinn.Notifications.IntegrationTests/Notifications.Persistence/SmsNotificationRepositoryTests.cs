using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class SmsNotificationRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToCleanup = [];

    private static async Task PersistSubstitutedSenderWithUnitOfWorkAsync(SmsNotificationRepository repo, Guid notificationId, string sender)
    {
        IUnitOfWorkRepository unitOfWorkRepository = (IUnitOfWorkRepository)ServiceUtil
            .GetServices([typeof(IUnitOfWorkRepository)])
            .First(i => i.GetType() == typeof(UnitOfWorkRepository));

        UnitOfWork unitOfWork = await unitOfWorkRepository.StartUnitOfWork();

        try
        {
            await repo.PersistSubstitutedSender(unitOfWork, notificationId, sender);
            await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
        }
        catch
        {
            await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            throw;
        }
    }

    private static async Task<Sms?> ClaimSingleNotificationWithUnitOfWorkAsync(
        SmsNotificationRepository repo,
        SendingTimePolicy sendingTimePolicy,
        bool commit)
    {
        IUnitOfWorkRepository unitOfWorkRepository = (IUnitOfWorkRepository)ServiceUtil
            .GetServices([typeof(IUnitOfWorkRepository)])
            .First(i => i.GetType() == typeof(UnitOfWorkRepository));

        UnitOfWork unitOfWork = await unitOfWorkRepository.StartUnitOfWork();

        try
        {
            Sms? sms = await repo.GetNewNotification(unitOfWork, TestContext.Current.CancellationToken, sendingTimePolicy);

            if (commit)
            {
                await unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
            }
            else
            {
                await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            }

            return sms;
        }
        catch
        {
            await unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_orderIdsToCleanup.Count == 0)
        {
            return;
        }

        foreach (var orderId in _orderIdsToCleanup)
        {
            await PostgreUtil.DeleteStatusFeedFromDb(orderId);
            await PostgreUtil.DeleteNotificationLogFromDb(orderId);
            await PostgreUtil.DeleteOrderFromDb(orderId);
        }
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetRecipients_ShouldReturnRecipientsForGivenOrderId()
    {
        // Arrange
        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);
        SmsRecipient expectedRecipient = smsNotification.Recipient;

        // Act
        List<SmsRecipient> actual = await repo.GetRecipients(order.Id);

        // Assert
        SmsRecipient actualRecipient = Assert.Single(actual);
        Assert.Equal(expectedRecipient.MobileNumber, actualRecipient.MobileNumber);
        Assert.Equal(expectedRecipient.OrganizationNumber, actualRecipient.OrganizationNumber);
        Assert.Equal(expectedRecipient.NationalIdentityNumber, actualRecipient.NationalIdentityNumber);
    }

    [Fact]
    public async Task AddNotification_ShouldInsertSmsNotificationIntoDatabase()
    {
        // Arrange
        Guid orderId = await PostgreUtil.PopulateDBWithSmsOrderAndReturnId();
        _orderIdsToCleanup.Add(orderId);

        // Arrange
        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        Guid smsNotificationId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;

        SmsNotification smsNotification = new()
        {
            OrderId = orderId,
            Id = smsNotificationId,
            RequestedSendTime = requestedSendTime.AddHours(2),

            Recipient = new()
            {
                MobileNumber = "+4799999999",
                NationalIdentityNumber = "16069412345",
                CustomizedBody = "Testing sending out an SMS to $recipientName"
            },

            SendResult = new NotificationResult<SmsNotificationResultType>(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        await repo.AddNotification(smsNotification, requestedSendTime.AddHours(50));

        // Assert
        string sql = $@"SELECT count(1) FROM notifications.smsnotifications s WHERE s.alternateid = '{smsNotificationId}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Theory]
    [InlineData("")] // Empty body
    [InlineData("Custom SMS Body")]
    public async Task GetNewNotification_WithEmptyCustomization_HandlesEmptyStringsCorrectly(string customBody)
    {
        // Arrange
        string defaultBody = "sms-body";

        SmsNotificationRepository sut = ServiceUtil
           .GetServices([typeof(ISmsNotificationRepository)])
           .OfType<SmsNotificationRepository>()
           .First();
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        await PostgreUtil.UpdateNotificationCustomizedContent<SmsNotification>(smsNotification.Id, null, customBody);

        // Act
        Sms? result = await ClaimSingleNotificationWithUnitOfWorkAsync(sut, SendingTimePolicy.Daytime, commit: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.IsNullOrEmpty(customBody) ? defaultBody : customBody, result.Message);
    }

    [Fact]
    public async Task GetNewNotification_ShouldReturnUnprocessedSmsNotification()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);

        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        Sms? smsToBeSent = await ClaimSingleNotificationWithUnitOfWorkAsync(repo, SendingTimePolicy.Anytime, commit: true);

        // Assert
        Assert.NotNull(smsToBeSent);
        Assert.Equal(smsNotification.Id, smsToBeSent.NotificationId);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public async Task GetNewNotification_ShouldReturnCreatorNameMatchingOrderCreator(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: sendingTimePolicy);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        Sms? smsToBeSent = await ClaimSingleNotificationWithUnitOfWorkAsync(repo, sendingTimePolicy, commit: true);

        // Assert
        Assert.NotNull(smsToBeSent);
        Assert.Equal(order.Creator.ShortName, smsToBeSent.Creator);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public async Task GetNewNotification_WithSendingPolicy_ShouldReturnEligibleSmsNotifications(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: sendingTimePolicy);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        Sms? smsToBeSent = await ClaimSingleNotificationWithUnitOfWorkAsync(repo, sendingTimePolicy, commit: true);

        // Assert
        Assert.NotNull(smsToBeSent);
        Assert.Equal(smsNotification.Id, smsToBeSent.NotificationId);
    }

    [Theory]
    [InlineData("10 seconds", false)]
    [InlineData("315 seconds", true)]
    public async Task TerminateExpiredNotifications_WithGracePeriod_UpdatesStatusBasedOnExpiryTime(string timeInterval, bool markedAsTTL)
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository sut = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        // modify the notification to simulate an expired notification
        await PostgreUtil.UpdateResultAndExpiryTimeNotification(smsNotification, timeInterval);

        // Act
        await sut.TerminateExpiredNotifications();

        // Assert
        var result = await SelectSmsNotificationStatus(smsNotification.Id);

        Assert.NotNull(result);

        if (markedAsTTL)
        {
            Assert.Equal(SmsNotificationResultType.Failed_TTL.ToString(), result);
        }
        else
        {
            Assert.Equal(SmsNotificationResultType.Accepted.ToString(), result);
        }
    }

    [Fact]
    public async Task GetNewNotification_ShouldTransitionStatusFromNewToSending_WhenUnitOfWorkCommits()
    {
        // Arrange
        List<Guid> claimedIds = [];
        SmsNotificationRepository repo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>().First();

        for (int i = 0; i < 3; i++)
        {
            (NotificationOrder order, SmsNotification sms) =
                await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);

            _orderIdsToCleanup.Add(order.Id);

            claimedIds.Add(sms.Id);
        }

        // Act
        Sms? claimed = await ClaimSingleNotificationWithUnitOfWorkAsync(repo, SendingTimePolicy.Anytime, commit: true);

        // Assert
        Assert.NotNull(claimed);
        Assert.Contains(claimedIds, id => id == claimed.NotificationId);
        string status = await SelectSmsNotificationStatus(claimed.NotificationId);
        Assert.Equal(SmsNotificationResultType.Sending.ToString(), status);
    }

    [Fact]
    public async Task GetNewNotification_SecondCommittedClaimShouldNotReturnAlreadyClaimed()
    {
        // Arrange
        const int count = 5;

        SmsNotificationRepository repo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>().First();

        List<Guid> notificationIds = [];

        for (int i = 0; i < count; i++)
        {
            (NotificationOrder order, SmsNotification sms) =
                await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);
            _orderIdsToCleanup.Add(order.Id);
            notificationIds.Add(sms.Id);
        }

        // Act
        Sms? firstClaim = await ClaimSingleNotificationWithUnitOfWorkAsync(repo, SendingTimePolicy.Anytime, commit: true);
        Sms? secondClaim = await ClaimSingleNotificationWithUnitOfWorkAsync(repo, SendingTimePolicy.Anytime, commit: true);

        // Assert
        Assert.NotNull(firstClaim);
        Assert.NotNull(secondClaim);
        Assert.NotEqual(firstClaim.NotificationId, secondClaim.NotificationId);
        Assert.Contains(firstClaim.NotificationId, notificationIds);
        Assert.Contains(secondClaim.NotificationId, notificationIds);
        string firstStatus = await SelectSmsNotificationStatus(firstClaim.NotificationId);
        string secondStatus = await SelectSmsNotificationStatus(secondClaim.NotificationId);
        Assert.Equal(SmsNotificationResultType.Sending.ToString(), firstStatus);
        Assert.Equal(SmsNotificationResultType.Sending.ToString(), secondStatus);
    }

    [Fact]
    public async Task GetNewNotification_WhenUnitOfWorkRollsBack_StatusRemainsNew()
    {
        // Arrange
        (NotificationOrder order, SmsNotification sms) =
            await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>().First();

        // Act
        Sms? claimed = await ClaimSingleNotificationWithUnitOfWorkAsync(repo, SendingTimePolicy.Anytime, commit: false);

        // Assert
        Assert.NotNull(claimed);
        string status = await SelectSmsNotificationStatus(sms.Id);
        Assert.Equal(SmsNotificationResultType.New.ToString(), status);
    }

    [Fact]
    public async Task GetNewNotification_WhenKeywordsAreUsed_ShouldAlwaysReturnCustomizedBody()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        SmsNotificationRepository sut = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();
        _orderIdsToCleanup.Add(order.Id);

        // Set customized value directly in the database to simulate keyword replacement
        string customizedBody = "Customized Body for Test";
        await PostgreUtil.UpdateNotificationCustomizedContent<SmsNotification>(smsNotification.Id, null, customizedBody);

        // Act
        Sms? itemWithCustomizedBody = await ClaimSingleNotificationWithUnitOfWorkAsync(sut, SendingTimePolicy.Daytime, commit: true);

        // Assert
        Assert.NotNull(itemWithCustomizedBody);
        Assert.Equal(customizedBody, itemWithCustomizedBody!.Message);
    }

    [Theory]
    [InlineData(SmsNotificationResultType.Failed)]
    [InlineData(SmsNotificationResultType.Failed_Deleted)]
    [InlineData(SmsNotificationResultType.Failed_Expired)]
    [InlineData(SmsNotificationResultType.Failed_Rejected)]
    [InlineData(SmsNotificationResultType.Failed_Undelivered)]
    [InlineData(SmsNotificationResultType.Failed_BarredReceiver)]
    [InlineData(SmsNotificationResultType.Failed_InvalidRecipient)]
    [InlineData(SmsNotificationResultType.Failed_RecipientReserved)]
    [InlineData(SmsNotificationResultType.Failed_RecipientNotIdentified)]
    public async Task ParseSmsSendOperationResult_StatusFailed_ShouldCompleteOrder_InsertStatusFeed_AndInsertNotificationLog(SmsNotificationResultType resultType)
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersReference: null, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        SmsSendOperationResult sendOperationResult = new()
        {
            SendResult = resultType,
            NotificationId = smsNotification.Id,
            GatewayReference = Guid.NewGuid().ToString()
        };

        // Act
        await repo.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference);

        // Assert
        var status = await SelectSmsNotificationStatus(smsNotification.Id);
        var completedCount = await SelectOrdersCompletedCount(order);
        var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);

        Assert.Equal(resultType.ToString(), status);
        Assert.Equal(1, completedCount);
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(1, notificationLogCount);
    }

    [Fact]
    public async Task TerminateExpiredNotifications_ShouldSetNotificationToFailed_CompleteOrder_InsertToFeed_AndInsertNotificationLog()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // modify the notification to simulate an expired notification
        string sql = $@"
            UPDATE notifications.smsnotifications
            SET result = 'Accepted',
                expirytime = NOW() - INTERVAL '3 day'
            WHERE alternateid = '{smsNotification.Id}';";

        await PostgreUtil.RunSql(sql);

        // Act
        await repo.TerminateExpiredNotifications();

        // Assert
        var result = await SelectSmsNotificationStatus(smsNotification.Id);
        var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);
        var orderStatus = await PostgreUtil.RunSqlReturnOutput<string>($"SELECT processedstatus FROM notifications.orders WHERE alternateid = '{order.Id}'");

        Assert.NotNull(result);
        Assert.Equal(SmsNotificationResultType.Failed_TTL.ToString(), result);
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(1, notificationLogCount);
        Assert.Equal(OrderProcessingStatus.Completed.ToString(), orderStatus);
    }

    [Fact]
    public async Task UpdateSendStatusDelivered_WithNotificationId_ShouldCompleteOrder_InsertStatusFeed_AndInsertNotificationLog()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        await repo.UpdateSendStatus(smsNotification.Id, SmsNotificationResultType.Delivered);

        // Assert
        var completedCount = await SelectOrdersCompletedCount(order);
        var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);

        Assert.Equal(1, completedCount);
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(1, notificationLogCount);
    }

    [Fact]
    public async Task UpdateSendStatusDelivered_WithGatewayRef_ShouldCompleteOrder_InsertStatusFeed_AndInsertNotificationLog()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        string gatewayReference = Guid.NewGuid().ToString();

        string setGatewaySql = $@"Update notifications.smsnotifications
                SET gatewayreference = '{gatewayReference}'
                WHERE alternateid = '{smsNotification.Id}'";

        await PostgreUtil.RunSql(setGatewaySql);

        // Act
        await repo.UpdateSendStatus(null, SmsNotificationResultType.Delivered, gatewayReference);

        // Assert
        var completedCount = await SelectOrdersCompletedCount(order);
        var statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var notificationLogCount = await PostgreUtil.SelectNotificationLogEntryCount(order.Id);

        Assert.Equal(1, completedCount);
        Assert.Equal(1, statusFeedCount);
        Assert.Equal(1, notificationLogCount);
    }

    [Fact]
    public async Task UpdateSendStatus_WithNotificationId_ShouldUpdateNotificationStatusAndGatewayReference()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        string gatewayReference = Guid.NewGuid().ToString();

        // Act
        await repo.UpdateSendStatus(smsNotification.Id, SmsNotificationResultType.Accepted, gatewayReference);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.smsnotifications sms
              WHERE sms.alternateid = '{smsNotification.Id}' 
              AND sms.result  = '{SmsNotificationResultType.Accepted}' 
              AND sms.gatewayreference = '{gatewayReference}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_WithNotificationId_ShouldUpdateNotificationStatus()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        await repo.UpdateSendStatus(smsNotification.Id, SmsNotificationResultType.Accepted);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.smsnotifications sms
              WHERE sms.alternateid = '{smsNotification.Id}' 
              AND sms.result  = '{SmsNotificationResultType.Accepted}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSmsNotificationResult_ShouldSupportAllEnumValuesInDatabase()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        // Act & Assert
        foreach (SmsNotificationResultType resultType in Enum.GetValues<SmsNotificationResultType>())
        {
            try
            {
                string sql = $@"
                UPDATE notifications.smsnotifications 
                SET result = '{resultType}'
                WHERE alternateid = '{smsNotification.Id}';";

                await PostgreUtil.RunSql(sql);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Exception thrown for SmsNotificationResultType: {resultType}. Exception: {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task TerminateExpiredNotifications_ShouldSetNotificationToFailed_CompleteOrder_AndInsertToFeed()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // modify the notification to simulate an expired notification
        string sql = $@"
            UPDATE notifications.smsnotifications 
            SET result = 'Accepted', 
                expirytime = NOW() - INTERVAL '3 day' 
            WHERE alternateid = '{smsNotification.Id}';";

        await PostgreUtil.RunSql(sql);

        // Act
        await repo.TerminateExpiredNotifications();

        // Assert
        var result = await SelectSmsNotificationStatus(smsNotification.Id);
        Assert.NotNull(result);
        Assert.Equal(SmsNotificationResultType.Failed_TTL.ToString(), result);

        var count = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, count); // Ensure that the status feed entry was created

        var orderStatus = await PostgreUtil.RunSqlReturnOutput<string>($"SELECT processedstatus FROM notifications.orders WHERE alternateid = '{order.Id}'");
        Assert.Equal(OrderProcessingStatus.Completed.ToString(), orderStatus);
    }

    [Fact]
    public async Task UpdateSendStatus_WithInvalidNotificationId_ThrowsInvalidNotificationIdentifierException()
    {
        // Arrange
        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        Guid emptyGuid = Guid.Empty;
        SmsNotificationResultType resultType = SmsNotificationResultType.Failed;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidNotificationIdentifierException>(async () =>
        {
            await repo.UpdateSendStatus(emptyGuid, resultType);
        });

        Assert.Equal("The provided SMS identifier is invalid.", exception.Message);
    }

    [Fact]
    public async Task UpdateSendStatus_WithNullNotificationIdAndWhitespaceGatewayReference_ThrowsInvalidNotificationIdentifierException()
    {
        // Arrange
        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidNotificationIdentifierException>(async () =>
        {
            await repo.UpdateSendStatus(null, SmsNotificationResultType.Accepted, "   ");
        });

        Assert.Equal("The provided SMS identifier is invalid.", exception.Message);
    }

    [Fact]
    public async Task UpdateSendStatus_WithoutNotificationId_WithGatewayRef()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        string gatewayReference = Guid.NewGuid().ToString();

        string setGateqwaySql = $@"Update notifications.smsnotifications 
                SET gatewayreference = '{gatewayReference}'
                WHERE alternateid = '{smsNotification.Id}'";

        await PostgreUtil.RunSql(setGateqwaySql);

        // Act
        await repo.UpdateSendStatus(Guid.Empty, SmsNotificationResultType.Accepted, gatewayReference);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.smsnotifications sms
              WHERE sms.alternateid = '{smsNotification.Id}'
              AND sms.result = '{SmsNotificationResultType.Accepted}'
              AND sms.gatewayreference = '{gatewayReference}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatusDelivered_WithNotificationId_OrderStatusIsSetToCompleted()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        await repo.UpdateSendStatus(smsNotification.Id, SmsNotificationResultType.Delivered);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.orders o
              WHERE o.alternateid = '{order.Id}'
              AND o.processedstatus = '{OrderProcessingStatus.Completed}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatusDelivered_WithGatewayRef_OrderStatusIsSetToCompleted()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        string gatewayReference = Guid.NewGuid().ToString();

        string setGateqwaySql = $@"Update notifications.smsnotifications 
                SET gatewayreference = '{gatewayReference}'
                WHERE alternateid = '{smsNotification.Id}'";

        await PostgreUtil.RunSql(setGateqwaySql);

        // Act
        await repo.UpdateSendStatus(null, SmsNotificationResultType.Delivered, gatewayReference);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.orders o
              WHERE o.alternateid = '{order.Id}'
              AND o.processedstatus = '{OrderProcessingStatus.Completed}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_UsingNonExistingGatewayRef_ThrowsNotificationNotFoundException()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
          .GetServices([typeof(ISmsNotificationRepository)])
          .First(i => i.GetType() == typeof(SmsNotificationRepository));

        string gatewayReference = Guid.NewGuid().ToString();
        string nonExistingGatewayReference = Guid.NewGuid().ToString();

        string setGateqwaySql = $@"Update notifications.smsnotifications 
                SET gatewayreference = '{gatewayReference}'
                WHERE alternateid = '{smsNotification.Id}'";

        await PostgreUtil.RunSql(setGateqwaySql);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotificationNotFoundException>(async () =>
        {
            await repo.UpdateSendStatus(
                notificationId: null,
                gatewayReference: nonExistingGatewayReference,
                result: SmsNotificationResultType.Delivered);
        });

        Assert.Equal($"Sms status update failed: GatewayReference='{nonExistingGatewayReference}' not found", exception.Message);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenExpiredNotification_ThrowsNotificationExpiredException()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        var expiryTime = DateTime.UtcNow.AddMinutes(-10);

        // Update the expiry time to be in the past (expired 10 minutes ago)
        string setExpirySql = $@"UPDATE notifications.smsnotifications
                SET expirytime = @expirytime
                WHERE alternateid = '{smsNotification.Id}'";
        await PostgreUtil.RunSql(setExpirySql, new Npgsql.NpgsqlParameter("@expirytime", expiryTime));

        // Act
        var ex = await Assert.ThrowsAsync<NotificationExpiredException>(() =>
            repo.UpdateSendStatus(smsNotification.Id, SmsNotificationResultType.Delivered, gatewayReference: null));

        // Assert: exception details
        Assert.Equal(NotificationChannel.Sms, ex.Channel);
        Assert.Equal(SendStatusIdentifierType.NotificationId, ex.IdentifierType);
        Assert.Equal(smsNotification.Id.ToString(), ex.Identifier);

        // Assert: notification status was not updated (remains New)
        string sql = $@"
        SELECT result
        FROM notifications.smsnotifications
        WHERE alternateid = '{smsNotification.Id}'";

        string result = await PostgreUtil.RunSqlReturnOutput<string>(sql);
        Assert.Equal(SmsNotificationResultType.New.ToString(), result);
    }

    private static async Task<int> SelectOrdersCompletedCount(NotificationOrder order)
    {
        string sql = $@"SELECT count(1) 
              FROM notifications.orders o
              WHERE o.alternateid = '{order.Id}'
              AND o.processedstatus = '{OrderProcessingStatus.Completed}'";
        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);
        return actualCount;
    }

    private static async Task<string> SelectSmsNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.smsnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }

    [Fact]
    public async Task UpdateSendStatus_WithDeliveryReport_PersistsDeliveryReportToDatabase()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        string gatewayReference = Guid.NewGuid().ToString();
        string deliveryReport = """{"messageId":"test-gw","status":"Delivered","deliveryStatusDetails":{"statusMessage":"OK"}}""";

        // Act
        await repo.UpdateSendStatus(smsNotification.Id, SmsNotificationResultType.Delivered, gatewayReference, deliveryReport);

        // Assert — result and gatewayReference updated
        string statusSql = $@"
            SELECT count(1) FROM notifications.smsnotifications
            WHERE alternateid = '{smsNotification.Id}'
              AND result = '{SmsNotificationResultType.Delivered}'
              AND gatewayreference = '{gatewayReference}'";

        int count = await PostgreUtil.RunSqlReturnOutput<int>(statusSql);
        Assert.Equal(1, count);

        // Assert — delivery report JSON is persisted and round-trips correctly
        string reportSql = $@"
            SELECT deliveryreport::text FROM notifications.smsnotifications
            WHERE alternateid = '{smsNotification.Id}'";

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
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        string gatewayReference = Guid.NewGuid().ToString();

        // Act — no delivery report supplied
        await repo.UpdateSendStatus(smsNotification.Id, SmsNotificationResultType.Accepted, gatewayReference, deliveryReport: null);

        // Assert — deliveryreport column should remain NULL
        string sql = $@"
            SELECT deliveryreport::text FROM notifications.smsnotifications
            WHERE alternateid = '{smsNotification.Id}'";

        string? persistedReport = await PostgreUtil.RunSqlReturnOutput<string?>(sql);
        Assert.Null(persistedReport);
    }

    [Fact]
    public async Task UpdateSendStatus_WithWhitespaceDeliveryReport_LeavesDeliveryReportColumnNull()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        await repo.UpdateSendStatus(
            smsNotification.Id,
            SmsNotificationResultType.Accepted,
            gatewayReference: Guid.NewGuid().ToString(),
            deliveryReport: "   ");

        // Assert
        string reportSql = $@"
            SELECT deliveryreport::text FROM notifications.smsnotifications
            WHERE alternateid = '{smsNotification.Id}'";

        string? persistedReport = await PostgreUtil.RunSqlReturnOutput<string?>(reportSql);
        Assert.Null(persistedReport);
    }

    [Fact]
    public async Task PersistSubstitutedSender_ValidNotificationId_PersistsSubstitutedSenderToDatabase()
    {
        if (!await HasSubstitutedSenderColumnAsync())
        {
            return;
        }

        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        const string substitutedSender = "+4775006000";

        // Act
        await PersistSubstitutedSenderWithUnitOfWorkAsync(repo, smsNotification.Id, substitutedSender);

        // Assert
        string sql = "SELECT substitutedsender FROM notifications.smsnotifications WHERE alternateid = @id";

        string? persistedSubstitutedSender = await PostgreUtil.RunSqlReturnOutput<string?>(sql, new NpgsqlParameter("@id", smsNotification.Id));
        Assert.Equal(substitutedSender, persistedSubstitutedSender);
    }

    [Fact]
    public async Task PersistSubstitutedSender_CalledTwice_OverwritesPreviousSubstitutedSender()
    {
        if (!await HasSubstitutedSenderColumnAsync())
        {
            return;
        }

        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        await PersistSubstitutedSenderWithUnitOfWorkAsync(repo, smsNotification.Id, "+4775006000");
        await PersistSubstitutedSenderWithUnitOfWorkAsync(repo, smsNotification.Id, "+4775006001");

        // Assert
        string sql = "SELECT substitutedsender FROM notifications.smsnotifications WHERE alternateid = @id";

        string? persistedSubstitutedSender = await PostgreUtil.RunSqlReturnOutput<string?>(sql, new NpgsqlParameter("@id", smsNotification.Id));
        Assert.Equal("+4775006001", persistedSubstitutedSender);
    }

    [Fact]
    public async Task PersistSubstitutedSender_UnknownNotificationId_DoesNotThrowAndLogsWarning()
    {
        if (!await HasSubstitutedSenderColumnAsync())
        {
            return;
        }

        // Arrange
        var loggerMock = new Mock<ILogger<SmsNotificationRepository>>();
        var repo = new SmsNotificationRepository(
            ServiceUtil.GetSharedDataSource(),
            loggerMock.Object,
            Options.Create(new NotificationConfig()));

        // Act — no matching row, but this is a best-effort write, so no exception is expected.
        await PersistSubstitutedSenderWithUnitOfWorkAsync(repo, Guid.NewGuid(), "+4775006000");

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                null,
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task PersistSubstitutedSender_InvalidUnitOfWork_ThrowsInvalidOperationExceptionWithInnerException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SmsNotificationRepository>>();
        var repo = new SmsNotificationRepository(
            ServiceUtil.GetSharedDataSource(),
            loggerMock.Object,
            Options.Create(new NotificationConfig()));

        UnitOfWork invalidUnitOfWork = new();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.PersistSubstitutedSender(invalidUnitOfWork, Guid.NewGuid(), "+4775006000"));

        // Assert
        Assert.Equal("Failed to persist substituted sender.", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task PersistSubstitutedSender_EmptyNotificationId_ThrowsInvalidOperationException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SmsNotificationRepository>>();
        var repo = new SmsNotificationRepository(
            ServiceUtil.GetSharedDataSource(),
            loggerMock.Object,
            Options.Create(new NotificationConfig()));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PersistSubstitutedSenderWithUnitOfWorkAsync(repo, Guid.Empty, "+4775006000"));

        // Assert
        Assert.IsType<InvalidNotificationIdentifierException>(exception.InnerException);
    }

    [Fact]
    public async Task PersistSubstitutedSender_NullSender_ThrowsArgumentNullExceptionBeforePersisting()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SmsNotificationRepository>>();
        var repo = new SmsNotificationRepository(
            ServiceUtil.GetSharedDataSource(),
            loggerMock.Object,
            Options.Create(new NotificationConfig()));

        // Act & Assert - validation happens before the try/catch, so the exception propagates
        // directly and is neither wrapped in InvalidOperationException nor logged.
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PersistSubstitutedSenderWithUnitOfWorkAsync(repo, Guid.NewGuid(), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PersistSubstitutedSender_EmptyOrWhitespaceSender_ThrowsArgumentExceptionBeforePersisting(string sender)
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SmsNotificationRepository>>();
        var repo = new SmsNotificationRepository(
            ServiceUtil.GetSharedDataSource(),
            loggerMock.Object,
            Options.Create(new NotificationConfig()));

        // Act & Assert - validation happens before the try/catch, so the exception propagates
        // directly and is neither wrapped in InvalidOperationException nor logged.
        await Assert.ThrowsAsync<ArgumentException>(
            () => PersistSubstitutedSenderWithUnitOfWorkAsync(repo, Guid.NewGuid(), sender));
    }

    private static async Task<bool> HasSubstitutedSenderColumnAsync()
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'notifications'
              AND table_name = 'smsnotifications'
              AND column_name = 'substitutedsender'";

        int count = await PostgreUtil.RunSqlReturnOutput<int>(sql);
        return count == 1;
    }
}
