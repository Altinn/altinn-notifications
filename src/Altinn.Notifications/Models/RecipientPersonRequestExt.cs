using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a request for sending notifications to a person identified by their national identity number.
/// </summary>
/// <remarks>
/// This class enables notifications to be sent to citizens through the Common Contact Register (KRR) integration,
/// supporting both email and SMS delivery channels based on the recipient's registered contact information.
/// </remarks>
public class RecipientPersonRequestExt
{
    /// <summary>
    /// Gets or sets the national identity number of the recipient.
    /// </summary>
    /// <remarks>
    /// Used to identify the person in the Common Contact Register (KRR) to obtain their registered contact information.
    /// </remarks>
    [Required]
    [JsonPropertyName("nationalIdentityNumber")]
    public required string NationalIdentityNumber { get; set; }

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
    /// Gets or sets a value indicating whether to bypass the recipient's reservation against electronic communication.
    /// </summary>
    /// <remarks>
    /// When set to <c>true</c>, notifications will be sent even if the recipient has reserved 
    /// themselves against electronic communication in the Common Contact Register (KRR). Defaults to <c>false</c>.
    /// </remarks>
    [JsonPropertyName("ignoreReservation")]
    public bool IgnoreReservation { get; set; } = false;

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
    /// Required when <see cref="ChannelScheme"/> is set to <see cref="NotificationChannelExt.Sms"/> or <see cref="NotificationChannelExt.SmsPreferred"/>.
    /// Contains SMS content, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("smsSettings")]
    public SmsSendingOptionsRequestExt? SmsSettings { get; set; }
}
