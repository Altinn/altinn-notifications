using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a model for sending notifications to a person identified by their national identity number.
/// </summary>
/// <remarks>
/// This class enables notifications to be sent to citizens through the Common Contact Register (KRR) integration,
/// supporting both email and SMS delivery channels based on the recipient's registered contact information.
/// </remarks>
public class RecipientPersonExt
{
    /// <summary>
    /// Gets or sets the national identity number of the recipient.
    /// </summary>
    /// <remarks>
    /// Used to identify the person in the Common Contact Register (KRR) to retrieve their registered contact information.
    /// </remarks>
    [Required]
    [JsonPropertyName("nationalIdentityNumber")]
    public required string NationalIdentityNumber { get; set; }

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
    /// </list>
    /// </remarks>
    [Required]
    [JsonPropertyName("channelSchema")]
    [DefaultValue(NotificationChannelExt.EmailPreferred)]
    public required NotificationChannelExt ChannelSchema { get; set; } = NotificationChannelExt.EmailPreferred;

    /// <summary>
    /// Gets or sets a value indicating whether to bypass the recipient's reservation against electronic communication.
    /// </summary>
    /// <remarks>
    /// When set to <c>true</c>, notifications will be sent even if the recipient has registered a reservation
    /// against electronic communication in the Common Contact Register (KRR).
    /// Defaults to <c>false</c>.
    /// </remarks>
    [JsonPropertyName("ignoreReservation")]
    public bool IgnoreReservation { get; set; } = false;

    /// <summary>
    /// Gets or sets the email-specific configuration, used when the channel scheme includes email.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannelExt.Email"/> 
    /// or <see cref="NotificationChannelExt.EmailPreferred"/>.
    /// Contains email content, subject, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("emailSettings")]
    public EmailSendingOptionsExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, used when the channel scheme includes SMS.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannelExt.Sms"/> 
    /// or <see cref="NotificationChannelExt.SmsPreferred"/>.
    /// Contains SMS content, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("smsSettings")]
    public SmsSendingOptionsExt? SmsSettings { get; set; }
}
