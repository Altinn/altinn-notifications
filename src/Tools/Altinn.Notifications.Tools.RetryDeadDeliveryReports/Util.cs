using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;

namespace Altinn.Notifications.Tools.RetryDeadDeliveryReports;

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

    /// <summary>
    /// Retrieves dead delivery reports from the repository, maps them to EmailSendOperationResult objects, and returns the list.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="fromId">Inclusive fromId, starting position</param>
    /// <param name="toId">Exclusive toId, ending position</param>
    /// <param name="channel">The delivery report channel to filter</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal static async Task<List<EmailSendOperationResult>> GetAndMapDeadDeliveryReports(
        IDeadDeliveryReportRepository repository,
        long fromId,
        long toId,
        Core.Enums.DeliveryReportChannel channel,
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
