using Altinn.Notifications.Core.Models.Files;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Extends <see cref="EmailSendingOptions"/> with an optional list of SAS-referenced files to be included when the email is sent.
/// </summary>
public record ComposedEmailSendingOptions : EmailSendingOptions
{
    /// <summary>
    /// The files to include in the email, each identified by a <see cref="SasFileReference"/>.
    /// When <see langword="null"/> or empty, the email is sent without attachments.
    /// </summary>
    public List<SasFileReference>? Attachments { get; init; }
}
