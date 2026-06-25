using Altinn.Notifications.Core.Models.Files;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Extends <see cref="EmailSendingOptions"/> with a list of SAS-referenced files to be included when the email is sent.
/// </summary>
public record ComposedEmailSendingOptions : EmailSendingOptions
{
    /// <summary>
    /// One or more files to include in the email, each identified by a <see cref="SasFileReference"/>.
    /// </summary>
    public required List<SasFileReference> Attachments { get; init; }
}
