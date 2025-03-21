using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a model for sending an email or SMS to a contact person
/// identified by an organization number, including configuration details.
/// </summary>
public class RecipientOrganization
{
    /// <summary>
    /// Gets or sets the organization number that identifies the recipient.
    /// </summary>
    /// <remarks>
    /// Used to identify the organization in the Norwegian Central Coordinating
    /// Register for Legal Entities (Enhetsregisteret) to retrieve their registered contact information.
    /// </remarks>
    public required string OrgNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier for authorization and auditing purposes.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the required channel scheme for delivering the notification.
    /// </summary>
    /// <remarks>
    /// Determines which communication channel(s) to use and their priority.
    /// Options include:
    /// <list type="bullet">
    /// <item><description><see cref="NotificationChannel.Email"/> - Use email only</description></item>
    /// <item><description><see cref="NotificationChannel.Sms"/> - Use SMS only</description></item>
    /// <item><description><see cref="NotificationChannel.EmailPreferred"/> - Try email first, fall back to SMS if email unavailable</description></item>
    /// <item><description><see cref="NotificationChannel.SmsPreferred"/> - Try SMS first, fall back to email if SMS unavailable</description></item>
    /// </list>
    /// </remarks>
    public required NotificationChannel ChannelScheme { get; set; }

    /// <summary>
    /// Gets or sets the email-specific configuration, used when the channel scheme includes email.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelScheme"/> is set to <see cref="NotificationChannel.Email"/> 
    /// or <see cref="NotificationChannel.EmailPreferred"/>.
    /// Contains email content, subject, sender information, and delivery preferences.
    /// </remarks>
    public EmailSendingOptions? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, used when the channel scheme includes SMS.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelScheme"/> is set to <see cref="NotificationChannel.Sms"/> 
    /// or <see cref="NotificationChannel.SmsPreferred"/>.
    /// Contains SMS content, sender information, and delivery preferences.
    /// </remarks>
    public SmsSendingOptions? SmsSettings { get; set; }
}
