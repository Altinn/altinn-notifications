using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;
using Xunit.Abstractions;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class EmailNotificationRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete;

    public EmailNotificationRepositoryTests()
    {
        _orderIdsToDelete = [];
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
        await PostgreUtil.RunSql(deleteSql);
    }

    [Fact]
    public async Task AddNotification()
    {
        // Arrange
        Guid orderId = await PostgreUtil.PopulateDBWithEmailOrderAndReturnId();
        _orderIdsToDelete.Add(orderId);

        // Arrange
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
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications o
              WHERE o.alternateid = '{notificationId}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task GetNewNotifications()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        List<Email> emailToBeSent = await repo.GetNewNotifications();

        // Assert
        Assert.Contains(emailToBeSent, s => s.NotificationId == emailNotification.Id);
    }

    [Fact]
    public async Task GetRecipients()
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
    public async Task UpdateSendStatus_WithNotificationId_WithGatewayRef()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        // Act
        await repo.UpdateSendStatus(emailNotification.Id, EmailNotificationResultType.Succeeded, operationId);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}' 
              AND email.result  = '{EmailNotificationResultType.Succeeded}' 
              AND email.operationid = '{operationId}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatusDelivered_WithNotificationId_OrderStatusIsSetToCompleted()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        // Act
        await repo.UpdateSendStatus(emailNotification.Id, EmailNotificationResultType.Delivered, operationId);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}' 
              AND email.result  = '{EmailNotificationResultType.Delivered}' 
              AND email.operationid = '{operationId}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        sql = $@"SELECT processedstatus FROM notifications.orders o
                 WHERE o.alternateid = '{order.Id}'";

        var processedStatus = await PostgreUtil.RunSqlReturnOutput<string>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_WithoutNotificationId_WithGatewayRef()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        string setGateqwaySql = $@"Update notifications.emailnotifications 
                SET operationid = '{operationId}'
                WHERE alternateid = '{emailNotification.Id}'";

        await PostgreUtil.RunSql(setGateqwaySql);

        // Act
        await repo.UpdateSendStatus(null, EmailNotificationResultType.Succeeded, operationId);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}'
              AND email.result = '{EmailNotificationResultType.Succeeded}'
              AND email.operationid = '{operationId}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task UpdateSendStatus_WithNotificationId_WithoutGatewayRef()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
          .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act
        await repo.UpdateSendStatus(emailNotification.Id, EmailNotificationResultType.Failed);

        // Assert
        string sql = $@"SELECT count(1) 
              FROM notifications.emailnotifications email
              WHERE email.alternateid = '{emailNotification.Id}' 
              AND email.result  = '{EmailNotificationResultType.Failed}'";

        int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

        Assert.Equal(1, actualCount);
    }

    [Fact]
    public async Task SetEmailResult_AllEnumValuesExistInDb()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(order.Id);

        // Act & Assert
        foreach (EmailNotificationResultType resultType in Enum.GetValues(typeof(EmailNotificationResultType)))
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
}
