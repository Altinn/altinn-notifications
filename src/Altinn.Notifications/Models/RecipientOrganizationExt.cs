using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a model for sending notifications to an organization's contact person.
/// </summary>
/// <remarks>
/// This class enables notifications to be sent to organizations through their registered
/// contact information in the Norwegian Central Coordinating Register for Legal Entities (Enhetsregisteret).
/// </remarks>
public class RecipientOrganizationExt
{
    /// <summary>
    /// Gets or sets the organization number that identifies the recipient.
    /// </summary>
    /// <remarks>
    /// Used to identify the organization in the Norwegian Central Coordinating
    /// Register for Legal Entities (Enhetsregisteret) to retrieve their registered contact information.
    /// </remarks>
    [Required]
    [JsonPropertyName("orgNumber")]
    public required string OrgNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier for authorization and auditing purposes.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the required channel scheme for delivering the notification.
    /// </summary>
    /// <remarks>
    /// Determines which communication channel(s) to use and their priority.
    /// Options include:
    /// <list type="bullet">
    /// <item><description><see cref="NotificationChannelExt.Email"/> - Use email only</description></item>
    /// <item><description><see cref="NotificationChannelExt.Sms"/> - Use SMS only</description></item>
    /// <item><description><see cref="NotificationChannelExt.EmailPreferred"/> - Try email first, fall back to SMS if email unavailable</description></item>
    /// <item><description><see cref="NotificationChannelExt.SmsPreferred"/> - Try SMS first, fall back to email if SMS unavailable</description></item>
    /// <item><description><see cref="NotificationChannelExt.EmailAndSms"/> - Use both email and SMS</description></item>
    /// </list>
    /// </remarks>
    [Required]
    [JsonPropertyName("channelSchema")]
    [DefaultValue(NotificationChannelExt.SmsPreferred)]
    public required NotificationChannelExt ChannelSchema { get; set; } = NotificationChannelExt.SmsPreferred;

    /// <summary>
    /// Gets or sets the email-specific configuration, used when the channel scheme includes email.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannelExt.Email"/>, 
    /// <see cref="NotificationChannelExt.EmailPreferred"/>, or <see cref="NotificationChannelExt.EmailAndSms"/>.
    /// Contains email content, subject, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("emailSettings")]
    public EmailSendingOptionsExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, used when the channel scheme includes SMS.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannelExt.Sms"/>, 
    /// <see cref="NotificationChannelExt.SmsPreferred"/>, or <see cref="NotificationChannelExt.EmailAndSms"/>.
    /// Contains SMS content, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("smsSettings")]
    public SmsSendingOptionsExt? SmsSettings { get; set; }
}
