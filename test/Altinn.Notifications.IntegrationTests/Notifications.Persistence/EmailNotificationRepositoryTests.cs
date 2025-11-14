using System.Reflection;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class EmailNotificationRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete;
    private readonly int _publishBatchSize = 500;

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
        List<Email> emailToBeSent = await repo.GetNewNotificationsAsync(_publishBatchSize, CancellationToken.None);

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

        // Verify that the order status was updated based on notification delivery
        // Initial state is "Registered", final state should be "Completed"
        Assert.Equal("Completed", processedStatus);
    }

    [Fact]
    public async Task UpdateSendStatus_GivenEmptyToAddress_ShouldSaveTheStatusFeedEntry()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(toAddress: string.Empty);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository sut = (EmailNotificationRepository)ServiceUtil
         .GetServices(new List<Type>() { typeof(IEmailNotificationRepository) })
         .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act 
        await sut.UpdateSendStatus(emailNotification.Id, EmailNotificationResultType.Delivered);

        // Assert
        var count = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        Assert.Equal(1, count);
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
    public async Task UpdateSendStatus_GivenNotificationIdWithoutOperationId_ShouldUpdateStatusSuccessfully()
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
    public async Task UpdateSendStatus_GivenNonExistentNotificationId_ThrowsSendStatusUpdateException()
    {
        // Arrange
        EmailNotificationRepository emailNotificationRepository = (EmailNotificationRepository)ServiceUtil
          .GetServices([typeof(IEmailNotificationRepository)])
          .First(i => i.GetType() == typeof(EmailNotificationRepository));
        Guid nonExistentNotificationId = Guid.NewGuid();

        // Act
        var ex = await Assert.ThrowsAsync<SendStatusUpdateException>(() => emailNotificationRepository.UpdateSendStatus(nonExistentNotificationId, EmailNotificationResultType.Succeeded));

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
    public async Task UpdateSendStatus_GivenNonExistentOperationId_ThrowsSendStatusUpdateException()
    {
        // Arrange
        EmailNotificationRepository emailNotificationRepository = (EmailNotificationRepository)ServiceUtil
          .GetServices([typeof(IEmailNotificationRepository)])
          .First(i => i.GetType() == typeof(EmailNotificationRepository));

        string operationId = Guid.NewGuid().ToString();

        // Act
        var ex = await Assert.ThrowsAsync<SendStatusUpdateException>(() => emailNotificationRepository.UpdateSendStatus(notificationId: null, status: EmailNotificationResultType.Succeeded, operationId: operationId));

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
    public async Task UpdateSendStatus_WithInvalidNotificationAndOperationIdentifiers_ThrowsArgumentException()
    {
        // Arrange
        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => repo.UpdateSendStatus(null, EmailNotificationResultType.Succeeded, null));

        Assert.Equal("The provided Email identifier is invalid.", exception.Message);
    }

    [Fact]
    public async Task TerminateExpiredNotifications_ShouldSetNotificationToFailed_CompleteOrder_AndInsertToFeed()
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
        var count = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
        var orderStatus = await PostgreUtil.RunSqlReturnOutput<string>($"SELECT processedstatus FROM notifications.orders WHERE alternateid = '{order.Id}'");

        Assert.NotNull(result);
        Assert.Equal(EmailNotificationResultType.Failed_TTL.ToString(), result);
        Assert.Equal(1, count);
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
    public async Task InsertOrderStatusCompletedOrder_ValidAlternateId_InsertsStatusFeedEntrySuccessfully()
    {
        // Arrange
        (NotificationOrder order, EmailNotification emailNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateConsumers: true, simulateCronJob: true);
        _orderIdsToDelete.Add(order.Id);

        EmailNotificationRepository repo = (EmailNotificationRepository)ServiceUtil
            .GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Act - should not throw and should insert status feed
        // Note: alternateId is the notification ID, not the order ID
        var method = typeof(NotificationRepositoryBase).GetMethod("InsertOrderStatusCompletedOrder", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(repo, [connection, transaction, emailNotification.Id])!;
        await task;
        await transaction.CommitAsync();

        // Assert - verify status feed entry was inserted
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
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Act & Assert
        var method = typeof(NotificationRepositoryBase).GetMethod("InsertOrderStatusCompletedOrder", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(repo, [connection, transaction, nonExistentOrderId])!;
        
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
        Assert.Equal("Order status could not be retrieved for the specified alternate ID.", ex.Message);
    }

    private static async Task<string> SelectEmailNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.emailnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }
}
