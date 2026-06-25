using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Validates a <see cref="RecipientComposedEmailExt"/> within a composed email order request.
/// </summary>
internal sealed class RecipientComposedEmailValidator : AbstractValidator<RecipientComposedEmailExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientComposedEmailValidator"/> class.
    /// </summary>
    public RecipientComposedEmailValidator()
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
                            rules.RuleFor(a => a.Filename)
                                .NotEmpty()
                                .WithMessage("Attachment filename must not be empty.");

                            rules.RuleFor(a => a.Filename)
                                .Must(SasFileReferenceRules.IsValidFilename)
                                .When(a => !string.IsNullOrWhiteSpace(a.Filename))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': filename must not contain path separators or traversal sequences, and must include a file extension.");

                            rules.RuleFor(a => a.SasUrl)
                                .NotEmpty()
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl must not be empty.");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(SasFileReferenceRules.IsAbsoluteHttpsUri)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl must be an absolute HTTPS URI.");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(SasFileReferenceRules.HasRequiredSasParameters)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl) && SasFileReferenceRules.IsAbsoluteHttpsUri(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl is missing required SAS parameters (se, sig, sp, sr).");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(url => SasFileReferenceRules.ParseSasExpiry(url) != null)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl) && SasFileReferenceRules.IsAbsoluteHttpsUri(a.SasUrl) && SasFileReferenceRules.HasRequiredSasParameters(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl has an invalid 'se' (signed expiry) value.");

                            rules.RuleFor(a => a.SasUrl)
                                .Must(SasFileReferenceRules.HasReadPermission)
                                .When(a => !string.IsNullOrWhiteSpace(a.SasUrl) && SasFileReferenceRules.IsAbsoluteHttpsUri(a.SasUrl) && SasFileReferenceRules.HasRequiredSasParameters(a.SasUrl))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': sasUrl does not grant read permission ('r' must be present in 'sp').");

                            rules.RuleFor(a => a.MimeType)
                                .NotEmpty()
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': mimeType must not be empty.");

                            rules.RuleFor(a => a.MimeType)
                                .Must(SasFileReferenceRules.IsAllowedMimeType)
                                .When(a => !string.IsNullOrWhiteSpace(a.MimeType))
                                .WithMessage((a, _) => $"Attachment '{a.Filename}': mimeType is not supported. Refer to ACS documentation for the list of accepted MIME types.");
                        });
                });
            });
        });
    }
}
