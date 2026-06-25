using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Email;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Defines the recipient and email settings for an email notification with file attachments.
/// </summary>
/// <remarks>
/// Used exclusively with the email-with-attachments order endpoint. The recipient is
/// identified by a direct email address rather than through KRR lookup.
/// </remarks>
public class RecipientEmailWithAttachmentsExt
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
