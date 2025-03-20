using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request for sending an email or SMS to a contact person
/// identified by an organization number, including configuration details.
/// </summary>
public class RecipientOrganizationRequestExt
{
    /// <summary>
    /// Gets or sets the organization number required to identify the contact person.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("orgNumber")]
    public required string OrgNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier used for referencing additional details.
    /// </summary>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the required channel scheme indicating how the notification
    /// should be delivered (e.g., email, SMS, email preferred, or SMS preferred)..
    /// </summary>
    [Required]
    [JsonPropertyOrder(3)]
    [JsonPropertyName("channelScheme")]
    public required NotificationChannelExt ChannelScheme { get; set; }

    /// <summary>
    /// Gets or sets optional email-specific template settings, if the chosen channel scheme includes email.
    /// </summary>
    [JsonPropertyOrder(5)]
    [JsonPropertyName("emailSettings")]
    public RecipientEmailSettingsRequestExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets optional SMS-specific template settings, if the chosen channel scheme includes SMS.
    /// </summary>
    [JsonPropertyOrder(6)]
    [JsonPropertyName("smsSettings")]
    public RecipientSmsSettingsRequestExt? SmsSettings { get; set; }
}
