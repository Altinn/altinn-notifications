using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Npgsql;

namespace Tools;

internal static class Util
{
    public static string RetryExceededReason => "RETRY_THRESHOLD_EXCEEDED";

    internal static EmailSendOperationResult? MapToEmailSendOperationResult(DeadDeliveryReport report)
    {
        try
        {
            var emailSendOperationResult = EmailSendOperationResult.Deserialize(report.DeliveryReport);
            return emailSendOperationResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deserializing DeliveryReport: {ex.Message}");
            return null;
        }
    }

    internal static async Task<List<EmailSendOperationResult>> GetAndMapDeadDeliveryReports(
        IDeadDeliveryReportRepository repository,
        int fromId,
        int toId,
        Altinn.Notifications.Core.Enums.DeliveryReportChannel channel,
        CancellationToken cancellationToken)
    {
        var reports = await repository.GetAllAsync(fromId, toId, RetryExceededReason, channel, cancellationToken);
        Console.WriteLine($"Found {reports.Count} dead delivery reports");

        var operationResults = reports
            .Select(report => MapToEmailSendOperationResult(report))
            .Where(result => result != null)
            .Cast<EmailSendOperationResult>()
            .ToList();

        return operationResults;
    }

    internal static async Task<bool> IsEmailNotificationDelivered(
        NpgsqlDataSource dataSource,
        string operationId)
    {
        const string sql = @"
            SELECT COUNT(1) 
            FROM notifications.emailnotifications 
            WHERE operationid = @operationId 
            AND result = 'Delivered'";

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("operationId", operationId);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    internal static async Task<int> ProduceMessagesToKafka(
        ICommonProducer producer,
        string? topic,
        List<EmailSendOperationResult> operationResults)
    {
        var successCount = 0;
        var failureCount = 0;

        if (string.IsNullOrEmpty(topic))
        {
            Console.WriteLine("Kafka topic is not configured.");
            return successCount;
        }

        foreach (var sendOperationResult in operationResults)
        {
            var result = await producer.ProduceAsync(topic, sendOperationResult.Serialize());
            
            if (result)
            {
                successCount++;
                Console.WriteLine($"✓ Message produced to topic {topic} for notification {sendOperationResult.NotificationId}");
            }
            else
            {
                failureCount++;
                Console.WriteLine($"✗ Failed to produce message to topic {topic} for notification {sendOperationResult.NotificationId}");
            }
        }

        Console.WriteLine($"\nSummary: {successCount} succeeded, {failureCount} failed out of {operationResults.Count} total");
        return successCount;
    }
}
