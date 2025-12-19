using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Npgsql;
using Tools.Kafka;

namespace Tools;

public static class Util
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
        long fromId,
        long toId,
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

    /// <summary>
    /// Maps email send operation result to Event Grid status string
    /// </summary>
    public static string? MapStatus(EmailSendOperationResult result)
    {
        if (string.Equals(result.SendResult.ToString(), "Failed_Bounced", StringComparison.OrdinalIgnoreCase))
        {
            return "Bounced";
        }
        if (string.Equals(result.SendResult.ToString(), "Failed_SupressedRecipient", StringComparison.OrdinalIgnoreCase))
        {
            return "Suppressed";
        }

        return result.SendResult.ToString();
    }
}
