using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents an email template that contains all the information needed to deliver an email to a specific address.
/// </summary>
public class RecipientEmailExt
{
    /// <summary>
    /// Gets or sets the email address to which the email should be sent.
    /// </summary>
    /// <value>The email address of the recipient.</value>
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the email settings.
    /// </summary>
    /// <value>The email settings, which include the template and sending time policy.</value>
    [Required]
    [JsonPropertyName("emailSettings")]
    public ScheduledEmailTemplateExt? EmailSettings { get; set; }
}
