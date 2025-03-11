using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a type that contains all the information needed to deliver either an email or SMS to a person identified by a national identity number.
/// </summary>
public class PersonRequestSettingsExt
{
    /// <summary>
    /// Gets or sets the national identity number of the recipient.
    /// </summary>
    [Required]
    [JsonPropertyName("nationalIdentityNumber")]
    public required string NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets the resource identifier to which the notification is related, and that recipient eligibility will be evaluated on.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore reservation against electronic communication.
    /// </summary>
    /// <value>
    /// If set to <c>true</c>, the reservation flag defined in KRR will not be respected, and the message is sent even to persons actively objecting to the use of digital channels.
    /// </value>
    [JsonPropertyName("ignoreReservation")]
    public bool IgnoreReservation { get; set; } = false;

    /// <summary>
    /// Gets or sets the communication channel scheme for the notification.
    /// </summary>
    [Required]
    [JsonPropertyName("channelScheme")]
    public required NotificationChannelExt ChannelScheme { get; set; }

    /// <summary>
    /// Gets or sets the email template settings for the notification.
    /// </summary>
    [JsonPropertyName("emailSettings")]
    public EmailRequestSettingsExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS template settings for the notification.
    /// </summary>
    [JsonPropertyName("smsSettings")]
    public SmsRequestSettingsExt? SmsSettings { get; set; }
}
