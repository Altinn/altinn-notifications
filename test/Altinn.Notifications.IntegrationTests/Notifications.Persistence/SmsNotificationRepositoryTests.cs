using Altinn.Notifications.Core.Enums;
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
    private readonly List<Guid> _orderIdsToDelete;

    public SmsNotificationRepositoryTests()
    {
        _orderIdsToDelete = [];
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_orderIdsToDelete.Count == 0)
        {
            return;
        }

        string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
        await PostgreUtil.RunSql(deleteSql);
    }

    [Fact]
    public async Task AddNotification()
    {
        // Arrange
        Guid orderId = await PostgreUtil.PopulateDBWithSmsOrderAndReturnId();
        _orderIdsToDelete.Add(orderId);

        // Arrange
        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(ISmsNotificationRepository) })
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

        Guid notificationId = Guid.NewGuid();
        SmsNotification smsNotification = new()
        {
            Id = notificationId,
            OrderId = orderId,
            RequestedSendTime = DateTime.UtcNow,
            Recipient = new()
            {
                NationalIdentityNumber = "16069412345",
                MobileNumber = "999999999"
            }
        };

        await repo.AddNotification(smsNotification, DateTime.UtcNow, 1);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.smsnotifications o
              WHERE o.alternateid = '{notificationId}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task GetNewNotifications()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToDelete.Add(order.Id);

        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(ISmsNotificationRepository) })
          .First(i => i.GetType() == typeof(SmsNotificationRepository));

        // Act
        List<Sms> smsToBeSent = await repo.GetNewNotifications();

        // Assert
        Assert.Contains(smsToBeSent, s => s.NotificationId == smsNotification.Id);
    }

    [Fact]
    public async Task GetRecipients()
    {
        // Arrange
        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
           .GetServices(new List<Type>() { typeof(ISmsNotificationRepository) })
           .First(i => i.GetType() == typeof(SmsNotificationRepository));

        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToDelete.Add(order.Id);
        SmsRecipient expectedRecipient = smsNotification.Recipient;

        // Act
        List<SmsRecipient> actual = await repo.GetRecipients(order.Id);

        SmsRecipient actualRecipient = actual[0];

        // Assert
        Assert.Single(actual);
        Assert.Equal(expectedRecipient.MobileNumber, actualRecipient.MobileNumber);
        Assert.Equal(expectedRecipient.NationalIdentityNumber, actualRecipient.NationalIdentityNumber);
        Assert.Equal(expectedRecipient.OrganizationNumber, actualRecipient.OrganizationNumber);
    }

    [Fact]
    public async Task UpdateSendStatus_WithNotificationId_WithGatewayRef()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToDelete.Add(order.Id);

        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(ISmsNotificationRepository) })
          .First(i => i.GetType() == typeof(SmsNotificationRepository));

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
    public async Task UpdateSendStatus_WithNotificationId_WithoutGatewayRef()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToDelete.Add(order.Id);

        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(ISmsNotificationRepository) })
          .First(i => i.GetType() == typeof(SmsNotificationRepository));

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
    public async Task SetSmsResult_AllEnumValuesExistInDb()
    {
        // Arrange
        (NotificationOrder order, SmsNotification smsNotification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToDelete.Add(order.Id);

        // Act & Assert
        foreach (SmsNotificationResultType resultType in Enum.GetValues(typeof(SmsNotificationResultType)))
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
    public async Task UpdateSendStatus_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
            .GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));

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
        _orderIdsToDelete.Add(order.Id);

        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(ISmsNotificationRepository) })
          .First(i => i.GetType() == typeof(SmsNotificationRepository));

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
}
