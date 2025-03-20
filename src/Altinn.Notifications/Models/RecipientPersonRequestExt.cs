using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request containing all the information needed to deliver either
/// an email or SMS to a specific person identified by their national identity number.
/// </summary>
public class RecipientPersonRequestExt
{
    /// <summary>
    /// Gets or sets the national identity number of the recipient.
    /// It is used to look up recipient information in the KRR registry.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("nationalIdentityNumber")]
    public required string NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier used for referencing additional details.
    /// </summary>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore the reservation flag
    /// for electronic communication (as defined in KRR).
    /// Defaults to <c>false</c>.
    /// </summary>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("ignoreReservation")]
    public bool IgnoreReservation { get; set; } = false;

    /// <summary>
    /// Gets or sets the required channel scheme for sending the notification
    /// (e.g., email, SMS, email preferred, or SMS preferred).
    /// </summary>
    [Required]
    [JsonPropertyOrder(4)]
    [JsonPropertyName("channelScheme")]
    public required NotificationChannelExt ChannelScheme { get; set; }

    /// <summary>
    /// Gets or sets optional email-specific template settings, if the chosen channel scheme includes email.
    /// </summary>
    [JsonPropertyOrder(5)]
    [JsonPropertyName("emailSettings")]
    public EmailSendingOptionsRequestExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets optional SMS-specific template settings, if the chosen channel scheme includes SMS.
    /// </summary>
    [JsonPropertyOrder(6)]
    [JsonPropertyName("smsSettings")]
    public SmsSendingOptionsRequestExt? SmsSettings { get; set; }
}
