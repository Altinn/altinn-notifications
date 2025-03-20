using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a request containing all the information needed to deliver either
/// an email or SMS to a specific person identified by their national identity number.
/// </summary>
public class PersonRecipientPayload
{
    /// <summary>
    /// Gets or sets the national identity number of the recipient.
    /// It is used to look up recipient information in the KRR registry.
    /// </summary>
    public required string NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier used for referencing additional details.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore the reservation flag
    /// for electronic communication (as defined in KRR).
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool IgnoreReservation { get; set; } = false;

    /// <summary>
    /// Gets or sets the required channel scheme for sending the notification
    /// (e.g., email, SMS, email preferred, or SMS preferred).
    /// </summary>
    public required NotificationChannel ChannelScheme { get; set; }

    /// <summary>
    /// Gets or sets optional email-specific template settings, if the chosen channel scheme includes email.
    /// </summary>
    public EmailRecipientPayloadSettings? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets optional SMS-specific template settings, if the chosen channel scheme includes SMS.
    /// </summary>
    public SmsRecipientPayloadSettings? SmsSettings { get; set; }
}
