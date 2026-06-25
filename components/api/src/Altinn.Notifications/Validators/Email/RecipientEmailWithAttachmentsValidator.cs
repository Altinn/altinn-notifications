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
                        .SetValidator(new EmailAttachmentValidator());
                });
            });
        });
    }
}
