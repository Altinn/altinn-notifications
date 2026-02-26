using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents the extracted delivery details from a notification recipient.
/// </summary>
internal sealed record RecipientDeliveryDetails
{
    /// <summary>
    /// Gets a list of recipients with proper addressing information.
    /// </summary>
    public required List<Recipient> Recipients { get; init; }

    /// <summary>
    /// Gets notification templates based on the recipient's configuration.
    /// </summary>
    public required List<INotificationTemplate> Templates { get; init; }

    /// <summary>
    /// Gets the determined notification channel based on recipient type.
    /// </summary>
    public required NotificationChannel Channel { get; init; }

    /// <summary>
    /// Gets a flag indicating whether to bypass KRR reservations.
    /// </summary>
    public bool? IgnoreReservation { get; init; }

    /// <summary>
    /// Gets an optional resource ID for authorization and tracking.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Gets the sending time policy associated with the SMS configuration.
    /// </summary>
    public SendingTimePolicy? SmsSendingTimePolicy { get; init; }

    /// <summary>
    /// Gets an empty <see cref="RecipientDeliveryDetails"/> instance with default values.
    /// </summary>
    /// <remarks>
    /// The <see cref="Channel"/> value is set to <see cref="NotificationChannel.Email"/> as a placeholder.
    /// This value is effectively inert since <see cref="Recipients"/> and <see cref="Templates"/> are empty,
    /// and consumers should always verify these collections before processing.
    /// </remarks>
    public static RecipientDeliveryDetails Empty { get; } = new()
    {
        Templates = [],
        Recipients = [],
        Channel = NotificationChannel.Email
    };
}
