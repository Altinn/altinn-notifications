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
}
