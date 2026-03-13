using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Provides utility methods for handling email notifications.
/// </summary>
/// <remarks>This class contains static methods that assist in processing email notification results, specifically
/// for parsing delivery status strings from Azure Communication Services.</remarks>
internal static class Utils
{
    /// <summary>
    /// Parses an ACS email delivery report status string to the corresponding <see cref="EmailNotificationResultType"/>.
    /// </summary>
    /// <param name="deliveryStatus">The delivery status string from Azure Communication Services.</param>
    /// <returns>The mapped <see cref="EmailNotificationResultType"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the delivery status is unrecognized.</exception>
    internal static EmailNotificationResultType ParseDeliveryStatus(string? deliveryStatus)
    {
        return deliveryStatus switch
        {
            null => EmailNotificationResultType.Failed,
            "Bounced" => EmailNotificationResultType.Failed_Bounced,
            "Delivered" => EmailNotificationResultType.Delivered,
            "Expanded" => EmailNotificationResultType.Failed,
            "Failed" => EmailNotificationResultType.Failed,
            "FilteredSpam" => EmailNotificationResultType.Failed_FilteredSpam,
            "Quarantined" => EmailNotificationResultType.Failed_Quarantined,
            "Suppressed" => EmailNotificationResultType.Failed_SupressedRecipient,
            _ => throw new ArgumentException($"Unhandled DeliveryStatus: {deliveryStatus}")
        };
    }
}
