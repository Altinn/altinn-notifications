using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Email;

/// <summary>
/// Validates individual <see cref="EmailAttachmentExt"/> objects within an email-with-attachments order.
/// </summary>
internal sealed class EmailAttachmentValidator : AbstractValidator<EmailAttachmentExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAttachmentValidator"/> class.
    /// </summary>
    public EmailAttachmentValidator()
    {
        RuleFor(a => a.Filename).ValidateAttachmentFilename();
        RuleFor(a => a.MimeType).ValidateAttachmentMimeType();
        RuleFor(a => a.SasUrl).ValidateAttachmentSasUrl();
    }
}
