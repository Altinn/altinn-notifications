using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class SmsNotificationRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToCleanup = [];

    public async Task DisposeAsync()
    {
        if (_orderIdsToCleanup.Count == 0)
        {
            return;
        }

        await Task.WhenAll(_orderIdsToCleanup.Select(PostgreUtil.DeleteOrderFromDb));
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
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

        await repo.AddNotification(smsNotification, requestedSendTime.AddHours(50), 1);

        // Assert
        string sql = $@"SELECT count(1) FROM notifications.smsnotifications s WHERE s.alternateid = '{smsNotificationId}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task GetNewNotifications_ShouldRespectBatchSize()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            (NotificationOrder order, SmsNotification _) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);
            _orderIdsToCleanup.Add(order.Id);
        }

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        List<Sms> smsToBeSent = await repo.GetNewNotifications(15, CancellationToken.None, SendingTimePolicy.Anytime);

        // Assert
        Assert.Equal(15, smsToBeSent.Count);
    }

    [Fact]
    public async Task GetNewNotifications_ShouldReturnUnprocessedSmsNotifications()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);

        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        List<Sms> smsToBeSent = await repo.GetNewNotifications(50, CancellationToken.None, SendingTimePolicy.Anytime);

        // Assert
        Assert.Contains(smsToBeSent, e => e.NotificationId == smsNotification.Id);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public async Task GetNewNotifications_WithSendingPolicy_ShouldReturnEligibleSmsNotifications(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: sendingTimePolicy);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        // Act
        List<Sms> smsToBeSent = await repo.GetNewNotifications(50, CancellationToken.None, sendingTimePolicy);

        // Assert
        Assert.Contains(smsToBeSent, s => s.NotificationId == smsNotification.Id);
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
    public async Task GetNewNotifications_ShouldTransitionStatusFromNewToSending()
    {
        // Arrange
        const int count = 3;
        List<Guid> claimedIds = [];
        SmsNotificationRepository repo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>().First();

        for (int i = 0; i < count; i++)
        {
            (NotificationOrder order, SmsNotification sms) =
                await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);
            
            _orderIdsToCleanup.Add(order.Id);

            claimedIds.Add(sms.Id);
        }

        // Act
        var claimed = await repo.GetNewNotifications(count, CancellationToken.None, SendingTimePolicy.Anytime);

        // Assert
        Assert.Equal(count, claimed.Count);

        foreach (var id in claimedIds)
        {
            Assert.Contains(claimed, c => c.NotificationId == id);
            string status = await SelectSmsNotificationStatus(id);
            Assert.Equal(SmsNotificationResultType.Sending.ToString(), status);
        }
    }

    [Fact]
    public async Task GetNewNotifications_SecondCallShouldNotReturnAlreadyClaimed()
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
        var firstBatch = await repo.GetNewNotifications(count, CancellationToken.None, SendingTimePolicy.Anytime);
        var secondBatch = await repo.GetNewNotifications(count, CancellationToken.None, SendingTimePolicy.Anytime);

        // Assert
        Assert.Equal(count, firstBatch.Count);
        Assert.Empty(secondBatch);

        foreach (var id in notificationIds)
        {
            string status = await SelectSmsNotificationStatus(id);
            Assert.Equal(SmsNotificationResultType.Sending.ToString(), status);
        }
    }

    [Fact]
    public async Task GetNewNotifications_BatchSizeZero_ShouldReturnEmpty_AndNotChangeState()
    {
        // Arrange
        (NotificationOrder order, SmsNotification sms) =
            await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);

        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>().First();

        // Act
        var result = await repo.GetNewNotifications(0, CancellationToken.None, SendingTimePolicy.Anytime);

        // Assert
        Assert.Empty(result);
        string status = await SelectSmsNotificationStatus(sms.Id);
        Assert.Equal(SmsNotificationResultType.New.ToString(), status);
    }

    [Fact]
    public async Task GetNewNotifications_BatchSizeNegative_ShouldReturnEmpty_AndNotChangeState()
    {
        // Arrange
        (NotificationOrder order, SmsNotification sms) =
            await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendingTimePolicy: SendingTimePolicy.Anytime);
        _orderIdsToCleanup.Add(order.Id);

        SmsNotificationRepository repo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>().First();

        // Act
        var result = await repo.GetNewNotifications(-10, CancellationToken.None, SendingTimePolicy.Anytime);

        // Assert
        Assert.Empty(result);
        string status = await SelectSmsNotificationStatus(sms.Id);
        Assert.Equal(SmsNotificationResultType.New.ToString(), status);
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
    public async Task ParseSmsSendOperationResult_StatusFailed_ShouldUpdateOrderStatusToCompleted(SmsNotificationResultType resultType)
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
        Assert.Equal(resultType.ToString(), status);

        int actualCount = await SelectOrdersCompletedCount(order);
        Assert.Equal(1, actualCount);
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
    public async Task UpdateSendStatus_WithInvalidNotificationId_ThrowsArgumentException()
    {
        // Arrange
        SmsNotificationRepository repo = ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        Guid emptyGuid = Guid.Empty;
        SmsNotificationResultType resultType = SmsNotificationResultType.Failed;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await repo.UpdateSendStatus(emptyGuid, resultType);
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

        // Update the expiry time to be in the past (expired 10 minutes ago)
        string setExpirySql = $@"UPDATE notifications.smsnotifications
                SET expirytime = '{DateTime.UtcNow.AddMinutes(-10):yyyy-MM-dd HH:mm:ss}'
                WHERE alternateid = '{smsNotification.Id}'";
        await PostgreUtil.RunSql(setExpirySql);

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
}
