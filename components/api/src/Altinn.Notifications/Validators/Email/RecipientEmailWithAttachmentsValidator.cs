using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Email;

/// <summary>
/// Validates a <see cref="RecipientEmailWithAttachmentsExt"/> object within an email-with-attachments order request.
/// </summary>
internal sealed class RecipientEmailWithAttachmentsValidator : AbstractValidator<RecipientEmailWithAttachmentsExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientEmailWithAttachmentsValidator"/> class.
    /// </summary>
    public RecipientEmailWithAttachmentsValidator()
    {
        RuleFor(e => e)
            .NotNull()
            .WithMessage("Recipient email object cannot be null.");

        When(e => e != null, () =>
        {
            RuleFor(e => e!.EmailAddress)
                .NotNull()
                .Must(RecipientRules.IsValidEmail)
                .WithMessage("Invalid email address format.");

            RuleFor(e => e!.Settings)
                .NotNull()
                .WithMessage("Recipient email settings cannot be null.");

            When(e => e!.Settings != null, () =>
            {
                RuleFor(e => e!.Settings)
                    .SetValidator(new EmailSendingOptionsValidator());

                RuleFor(e => e!.Settings.Attachments)
                    .NotEmpty()
                    .WithMessage("At least one attachment is required.");

                When(e => e!.Settings.Attachments is { Count: > 0 }, () =>
                {
                    RuleForEach(e => e!.Settings.Attachments)
                        .NotNull()
                        .WithMessage("Attachment item must not be null.");

                    RuleForEach(e => e!.Settings.Attachments)
                        .ChildRules(rules =>
                        {
                            rules.RuleFor(a => a.SasUrl)
                                .NotEmpty()
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl must not be empty.");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(EmailAttachmentRules.IsAbsoluteHttpsUri)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl must be an absolute HTTPS URI.");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(EmailAttachmentRules.HasRequiredSasParameters)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl) && EmailAttachmentRules.IsAbsoluteHttpsUri(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl is missing required SAS parameters (se, sig, sp, sr).");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(url => EmailAttachmentRules.ParseSasExpiry(url) != null)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl) && EmailAttachmentRules.IsAbsoluteHttpsUri(a.SasUrl) && EmailAttachmentRules.HasRequiredSasParameters(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl has an invalid 'se' (signed expiry) value.");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(EmailAttachmentRules.HasReadPermission)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl) && EmailAttachmentRules.IsAbsoluteHttpsUri(a.SasUrl) && EmailAttachmentRules.HasRequiredSasParameters(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl does not grant read permission ('r' must be present in 'sp').");

                            rules.RuleFor(a => a.Filename)
                                .NotEmpty()
                                .WithMessage("Attachment filename must not be empty.");

                            rules.RuleFor(a => a.Filename)
                                .Must(EmailAttachmentRules.IsValidFilename)
                                .When(a => !string.IsNullOrWhiteSpace(a.Filename))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': filename must not contain path separators or traversal sequences, and must include a file extension.");

                            rules.RuleFor(a => a.MimeType)
                                .NotEmpty()
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': mimeType must not be empty.");

                            rules.RuleFor(a => a.MimeType)
                                .Must(EmailAttachmentRules.IsAllowedMimeType)
                                .When(a => !string.IsNullOrWhiteSpace(a.MimeType))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': mimeType is not supported. Refer to ACS documentation for the list of accepted MIME types.");
                        });
                });
            });
        });
    }
}
