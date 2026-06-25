using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Email;

/// <summary>
/// Defines the sending options for an email with one or more file attachments.
/// </summary>
/// <remarks>
/// Inherits all standard email sending options from <see cref="EmailSendingOptionsExt"/>
/// and extends them with a required list of attachments. Each attachment is referenced
/// by a SAS URL and fetched by the email service at send time.
/// </remarks>
public class EmailWithAttachmentsSendingOptionsExt : EmailSendingOptionsExt
{
    /// <summary>
    /// One or more file attachments to include in the email.
    /// </summary>
    [Required]
    [JsonPropertyName("attachments")]
    public required List<EmailAttachmentExt> Attachments { get; init; }
}
