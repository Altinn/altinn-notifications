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
            RuleFor(details => details.EmailSettings).ChildRules(settings =>
            {
                settings.When(e => e.SenderEmailAddress != null, () =>
                {
                    settings.RuleFor(e => e.SenderEmailAddress)
                        .Cascade(CascadeMode.Stop)
                        .Must(RecipientRules.IsValidEmail)
                        .WithMessage("Invalid email address format.")
                        .MustBeAllowedEmailSender();
                });

                settings.RuleFor(e => e.Subject)
                    .NotEmpty()
                    .WithMessage("The email subject cannot be empty.");

                settings.RuleFor(e => e.Body)
                    .NotEmpty()
                    .WithMessage("The email body cannot be empty.");

                settings.RuleFor(e => e.ContentType)
                    .IsInEnum()
                    .WithMessage("Email content type must be either Plain or HTML.");
            });
        });
    }
}
