using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Rules;
using FluentValidation;

namespace Altinn.Notifications.Validators.Email;

/// <summary>
/// Represents validation logic for the recipient email associated with request model.
/// </summary>
internal sealed class RecipientEmailValidator : AbstractValidator<RecipientEmailExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientEmailValidator"/> class.
    /// </summary>
    public RecipientEmailValidator()
    {
        RuleFor(recipient => recipient)
            .NotNull()
            .WithMessage("Recipient email object cannot be null.");

        When(recipient => recipient != null, () =>
        {
            RuleFor(recipient => recipient!.EmailAddress)
                .NotNull()
                .Must(RecipientRules.IsValidEmail)
                .WithMessage("Invalid email address format.");

            RuleFor(recipient => recipient!.Settings)
                .NotNull()
                .WithMessage("Recipient email settings cannot be null.")
                .SetValidator(new EmailSendingOptionsValidator());
        });
    }
}
