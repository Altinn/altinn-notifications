using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a request for sending an email or SMS to a contact person
/// identified by an organization number, including configuration details.
/// </summary>
public class OrganizationRecipientPayload
{
    /// <summary>
    /// Gets or sets the organization number required to identify the contact person.
    /// </summary>
    public required string OrgNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier used for referencing additional details.
    /// </summary>
    public string? ResourceId { get; set; }

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
