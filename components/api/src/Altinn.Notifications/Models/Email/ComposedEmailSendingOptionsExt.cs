using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Files;

namespace Altinn.Notifications.Models.Email;

/// <summary>
/// Extends <see cref="EmailSendingOptionsExt"/> with an optional list of SAS-referenced files
/// to be included when the email is sent.
/// </summary>
public class ComposedEmailSendingOptionsExt : EmailSendingOptionsExt
{
    /// <summary>
    /// The files to include in the email, each identified by a <see cref="SasFileReferenceExt"/>.
    /// </summary>
    [JsonPropertyName("attachments")]
    public List<SasFileReferenceExt>? Attachments { get; init; }
}
