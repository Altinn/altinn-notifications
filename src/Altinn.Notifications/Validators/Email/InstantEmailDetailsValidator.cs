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
        RuleFor(deliveryDetails => deliveryDetails.EmailAddress)
            .Must(RecipientRules.IsValidEmail)
            .WithMessage("The recipient email address is not a valid email address.");

        RuleFor(deliveryDetails => deliveryDetails.EmailSettings)
            .NotNull()
            .WithMessage("Email settings cannot be null.")
            .ChildRules(emailSettings =>
            {
                emailSettings.When(settings => settings.SenderEmailAddress != null, () =>
                {
                    emailSettings.RuleFor(settings => settings.SenderEmailAddress)
                        .Must(RecipientRules.IsValidEmail!)
                        .WithMessage("The sender email address is not a valid email address.");
                });

                emailSettings.RuleFor(settings => settings.Subject)
                    .NotEmpty()
                    .WithMessage("The email subject must not be empty.");

                emailSettings.RuleFor(settings => settings.Body)
                    .NotEmpty()
                    .WithMessage("The email body must not be empty.");

                emailSettings.RuleFor(settings => settings.ContentType)
                    .IsInEnum()
                    .WithMessage("Email content type must be either Plain or HTML.");
            });
    }
}
