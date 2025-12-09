using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;

namespace Tools;

internal static class Util
{

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
}
