using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request for sending an email to a specific recipient.
/// </summary>
public class RecipientEmailRequestExt
{
    /// <summary>
    /// Gets or sets the email address of the intended recipient.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the configuration for the email, including the subject, body, and sending time policy.
    /// </summary>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("emailSettings")]
    public required RecipientEmailSettingsRequestExt Settings { get; set; }
}
