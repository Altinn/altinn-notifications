using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class SmsNotificationRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> orderIdsToDelete;

    public SmsNotificationRepositoryTests()
    {
        orderIdsToDelete = [];
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", orderIdsToDelete)}')";
        await PostgreUtil.RunSql(deleteSql);
    }

    [Fact]
    public async Task AddNotification()
    {
        // Arrange
        Guid orderId = await PostgreUtil.PopulateDBWithOrderAndReturnId();
        orderIdsToDelete.Add(orderId);

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
            RecipientId = "12345678",
            RecipientNumber = "999999999",
        };

        await repo.AddNotification(smsNotification, DateTime.UtcNow);

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
        orderIdsToDelete.Add(order.Id);

        SmsNotificationRepository repo = (SmsNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(ISmsNotificationRepository) })
          .First(i => i.GetType() == typeof(SmsNotificationRepository));

        // Act
        List<Sms> smsToBeSent = await repo.GetNewNotifications();

        // Assert
        Assert.NotEmpty(smsToBeSent.Where(s => s.NotificationId == smsNotification.Id));
    }
}
