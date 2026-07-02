using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Email;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Identifies a recipient for a composed email notification by their email address
/// and the associated sending options, including SAS-referenced files.
/// </summary>
/// <remarks>
/// The recipient is identified by a direct email address; no KRR lookup is performed.
/// </remarks>
public class RecipientComposedEmailExt
{
    /// <summary>
    /// The email address of the recipient.
    /// </summary>
    [Required]
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; init; }

    /// <summary>
    /// The email sending options, including subject, body, and SAS-referenced files to include.
    /// </summary>
    [Required]
    [JsonPropertyName("emailSettings")]
    public required ComposedEmailSendingOptionsExt Settings { get; init; }
}
