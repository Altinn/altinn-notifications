using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Email;

/// <summary>
/// Represents validation logic for instant email details including recipient and content validation.
/// </summary>
internal sealed class InstantEmailDetailsValidator : AbstractValidator<InstantEmailDetailsExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantEmailDetailsValidator"/> class.
    /// </summary>
    public InstantEmailDetailsValidator()
    {
        RuleFor(details => details.EmailAddress)
            .Must(RecipientRules.IsValidEmail)
            .WithMessage("Invalid email address format.");

        RuleFor(details => details.EmailSettings)
            .NotNull()
            .WithMessage("Email settings cannot be null.");

        When(details => details.EmailSettings != null, () =>
        {
            When(details => details.EmailSettings!.SenderEmailAddress != null, () =>
            {
                RuleFor(details => details.EmailSettings!.SenderEmailAddress)
                    .Must(RecipientRules.IsValidEmail!)
                    .WithMessage("Invalid email address format.");
            });

            RuleFor(details => details.EmailSettings!.Subject)
                .NotEmpty()
                .WithMessage("The email subject cannot be empty.");

            RuleFor(details => details.EmailSettings!.Body)
                .NotEmpty()
                .WithMessage("The email body cannot be empty.");

            RuleFor(details => details.EmailSettings!.ContentType)
                .IsInEnum()
                .WithMessage("Email content type must be either Plain or HTML.");
        });
    }
}
