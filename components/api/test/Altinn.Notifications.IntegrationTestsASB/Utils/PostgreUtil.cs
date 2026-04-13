using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Altinn.Notifications.IntegrationTestsASB.Utils;

/// <summary>
/// Database utility methods for ASB integration tests.
/// Uses the factory's DI-resolved repositories and data source.
/// </summary>
public static class PostgreUtil
{
    /// <summary>
    /// Creates an order and email notification in the database, simulating the full cron job + consumer flow.
    /// </summary>
    public static async Task<(NotificationOrder Order, EmailNotification Notification)> PopulateDBWithOrderAndEmailNotification(
        IntegrationTestWebApplicationFactory factory,
        string? toAddress = null,
        DateTime? expiry = null)
    {
        (NotificationOrder order, EmailNotification notification) = TestdataUtil.GetOrderAndEmailNotification();

        if (toAddress != null)
        {
            notification.Recipient.ToAddress = toAddress;
        }

        using var scope = factory.Host.Services.CreateScope();
        var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailNotificationRepository>();

        await orderRepo.Create(order);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
        await emailRepo.AddNotification(notification, expiry ?? DateTime.UtcNow.AddDays(1));
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);

        return (order, notification);
    }

    /// <summary>
    /// Updates the send status of an email notification.
    /// </summary>
    public static async Task UpdateSendStatus(
        IntegrationTestWebApplicationFactory factory,
        Guid notificationId,
        EmailNotificationResultType resultType,
        string? operationId = null)
    {
        using var scope = factory.Host.Services.CreateScope();
        var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailNotificationRepository>();
        await emailRepo.UpdateSendStatus(notificationId, resultType, operationId);
    }

    /// <summary>
    /// Executes a raw SQL query against the test database.
    /// </summary>
    public static async Task RunSql(string connectionString, string sql, params NpgsqlParameter[] parameters)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var cmd = dataSource.CreateCommand(sql);

        if (parameters.Length > 0)
        {
            cmd.Parameters.AddRange(parameters);
        }

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes a raw SQL query and returns a scalar value.
    /// </summary>
    public static async Task<T> RunSqlReturnOutput<T>(string connectionString, string sql, params NpgsqlParameter[] parameters)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var cmd = dataSource.CreateCommand(sql);

        if (parameters.Length > 0)
        {
            cmd.Parameters.AddRange(parameters);
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Query returned no rows.");
        }

        return await reader.GetFieldValueAsync<T>(0);
    }

    /// <summary>
    /// Creates an order and SMS notification in the database.
    /// </summary>
    public static async Task<(NotificationOrder Order, SmsNotification Notification)> PopulateDBWithOrderAndSmsNotification(
        IntegrationTestWebApplicationFactory factory,
        DateTime? expiry = null)
    {
        (NotificationOrder order, SmsNotification notification) = TestdataUtil.GetOrderAndSmsNotification();

        using var scope = factory.Host.Services.CreateScope();
        var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var smsRepo = scope.ServiceProvider.GetRequiredService<ISmsNotificationRepository>();

        await orderRepo.Create(order);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
        await smsRepo.AddNotification(notification, expiry ?? DateTime.UtcNow.AddDays(1), 0);
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processed);

        return (order, notification);
    }

    /// <summary>
    /// Updates the send status of an SMS notification, simulating the SMS service having accepted the message.
    /// Sets the gateway reference so that the delivery report handler can find the notification.
    /// </summary>
    public static async Task UpdateSmsSendStatus(
        IntegrationTestWebApplicationFactory factory,
        Guid notificationId,
        SmsNotificationResultType resultType,
        string? gatewayReference = null)
    {
        using var scope = factory.Host.Services.CreateScope();
        var smsRepo = scope.ServiceProvider.GetRequiredService<ISmsNotificationRepository>();
        await smsRepo.UpdateSendStatus(notificationId, resultType, gatewayReference);
    }

    /// <summary>
    /// Looks up a dead delivery report by the gatewayReference stored in the JSONB deliveryreport column.
    /// </summary>
    public static async Task<DeadDeliveryReportRow?> GetDeadDeliveryReportByGatewayReference(string connectionString, string gatewayReference)
    {
        const string sql = """
            SELECT id, channel, reason, attemptcount, resolved
            FROM notifications.deaddeliveryreports
            WHERE deliveryreport ->> 'gatewayReference' = @ref
            ORDER BY id DESC
            LIMIT 1
            """;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("ref", gatewayReference);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DeadDeliveryReportRow(
            Id: await reader.GetFieldValueAsync<long>(0),
            Channel: (DeliveryReportChannel)await reader.GetFieldValueAsync<short>(1),
            Reason: await reader.IsDBNullAsync(2) ? null : await reader.GetFieldValueAsync<string>(2),
            AttemptCount: await reader.GetFieldValueAsync<int>(3),
            Resolved: await reader.GetFieldValueAsync<bool>(4));
    }

    /// <summary>
    /// Looks up a dead delivery report by the messageId stored in the JSONB deliveryreport column.
    /// </summary>
    public static async Task<DeadDeliveryReportRow?> GetDeadDeliveryReportByMessageId(string connectionString, string messageId)
    {
        const string sql = """
            SELECT id, channel, reason, attemptcount, resolved
            FROM notifications.deaddeliveryreports
            WHERE deliveryreport ->> 'messageId' = @messageId
            ORDER BY id DESC
            LIMIT 1
            """;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("messageId", messageId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DeadDeliveryReportRow(
            Id: await reader.GetFieldValueAsync<long>(0),
            Channel: (DeliveryReportChannel)await reader.GetFieldValueAsync<short>(1),
            Reason: await reader.IsDBNullAsync(2) ? null : await reader.GetFieldValueAsync<string>(2),
            AttemptCount: await reader.GetFieldValueAsync<int>(3),
            Resolved: await reader.GetFieldValueAsync<bool>(4));
    }
}
