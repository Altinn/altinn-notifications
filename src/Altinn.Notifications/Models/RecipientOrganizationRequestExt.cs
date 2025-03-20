using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a request for sending notifications to an organization's contact person.
/// </summary>
/// <remarks>
/// This class enables notifications to be sent to organizations through their registered
/// contact information in the Norwegian Central Coordinating Register for Legal Entities.
/// Supports both email and SMS delivery channels based on the organization's preferences.
/// </remarks>
public class RecipientOrganizationRequestExt
{
    /// <summary>
    /// Gets or sets the organization number that identifies the recipient.
    /// </summary>
    /// <remarks>
    /// Used to identify the organization in the Central Coordinating Register for Legal Entities
    /// to obtain their registered contact information.
    /// </remarks>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("orgNumber")]
    public required string OrgNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier for authorization and auditing purposes.
    /// </summary>
    /// <remarks>
    /// When provided, this identifier helps link the notification to a specific resource in other systems,
    /// enabling authorization checks and establishing context for the notification.
    /// </remarks>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the required channel scheme for delivering the notification.
    /// </summary>
    /// <remarks>
    /// Determines which communication channel(s) to use and their priority.
    /// </remarks>
    [Required]
    [JsonPropertyOrder(3)]
    [JsonPropertyName("channelScheme")]
    public required NotificationChannelExt ChannelScheme { get; set; }

    /// <summary>
    /// Gets or sets the email-specific configuration, used when the channel scheme includes email.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelScheme"/> is set to <see cref="NotificationChannelExt.Email"/>
    /// or <see cref="NotificationChannelExt.EmailPreferred"/>.
    /// </remarks>
    [JsonPropertyOrder(4)]
    [JsonPropertyName("emailSettings")]
    public EmailSendingOptionsRequestExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, used when the channel scheme includes SMS.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelScheme"/> is set to <see cref="NotificationChannelExt.Sms"/>
    /// or <see cref="NotificationChannelExt.SmsPreferred"/>.
    /// </remarks>
    [JsonPropertyOrder(5)]
    [JsonPropertyName("smsSettings")]
    public SmsSendingOptionsRequestExt? SmsSettings { get; set; }
}
