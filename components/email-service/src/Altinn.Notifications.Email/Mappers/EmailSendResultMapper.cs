using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Mappers;

/// <summary>
/// Mapper handling parsing to EmailSendResult
/// </summary>
public static class EmailSendResultMapper
{
    /// <summary>
    /// Parse AcsEmailDeliveryReportStatus to EmailSendResult
    /// </summary>
    /// <param name="deliveryStatus">Delivery status from Azure Communication Service</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Throws exception if unknown delivery status</exception>
    public static EmailSendResult ParseDeliveryStatus(string? deliveryStatus)
    {
        switch (deliveryStatus)
        {
            case null: 
                return EmailSendResult.Failed;
            case "Bounced":
                return EmailSendResult.Failed_Bounced;
            case "Delivered":
                return EmailSendResult.Delivered;
            case "Failed":
                return EmailSendResult.Failed;
            case "FilteredSpam":
                return EmailSendResult.Failed_FilteredSpam;
            case "Quarantined":
                return EmailSendResult.Failed_Quarantined;
            case "Suppressed":
                return EmailSendResult.Failed_SupressedRecipient;
            default:
                throw new ArgumentException($"Unhandled DeliveryStatus: {deliveryStatus}");
        }
    }
}
