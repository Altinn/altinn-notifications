using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a request for sending notifications to an organization's contact person.
/// </summary>
/// <remarks>
/// This class enables notifications to be sent to organizations through their registered
/// contact information in the Norwegian Central Coordinating Register for Legal Entities (Einingsregisteret).
/// Supports both email and SMS delivery channels based on the organization's preferences.
/// </remarks>
public class RecipientOrganizationRequestExt
{
    /// <summary>
    /// Gets or sets the organization number that identifies the recipient.
    /// </summary>
    /// <remarks>
    /// Used to identify the organization in the Norwegian Central Coordinating
    /// Register for Legal Entities (Einingsregisteret) to obtain their registered contact information.
    /// </remarks>
    [Required]
    [JsonPropertyName("orgNumber")]
    public required string OrgNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier for auditing purposes.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the required channel scheme for delivering the notification.
    /// </summary>
    /// <remarks>
    /// Determines which communication channel(s) to use and their priority.
    /// </remarks>
    [Required]
    [JsonPropertyName("channelScheme")]
    public required NotificationChannelExt ChannelScheme { get; set; }

    /// <summary>
    /// Gets or sets the email-specific configuration, used when the channel scheme includes email.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelScheme"/> is set to <see cref="NotificationChannelExt.Email"/> or <see cref="NotificationChannelExt.EmailPreferred"/>.
    /// Contains email content, subject, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("emailSettings")]
    public EmailSendingOptionsRequestExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, used when the channel scheme includes SMS.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelScheme"/> is set to <see cref="NotificationChannelExt.Sms"/> or <see cref="NotificationChannelExt.SmsPreferred"/>..
    /// Contains SMS content, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("smsSettings")]
    public SmsSendingOptionsRequestExt? SmsSettings { get; set; }
}
