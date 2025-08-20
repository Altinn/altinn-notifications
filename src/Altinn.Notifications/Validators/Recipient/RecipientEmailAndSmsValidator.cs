using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Represents validation logic for the recipient email and SMS model.
/// </summary>
public class RecipientEmailAndSmsValidator : AbstractValidator<RecipientEmailAndSmsExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientEmailAndSmsValidator"/> class.
    /// </summary>
    public RecipientEmailAndSmsValidator()
    {
        RuleFor(recipient => recipient!.EmailAddress)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Email address must be a valid email address.");

        RuleFor(recipient => recipient!.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required.");

        RuleFor(recipient => recipient!.EmailSettings)
            .NotNull()
            .WithMessage("Email settings are required.")
            .SetValidator(new EmailSendingOptionsValidator());

        RuleFor(recipient => recipient!.SmsSettings)
            .NotNull()
            .WithMessage("SMS settings are required.")
            .SetValidator(new SmsSendingOptionsValidator());
    }
}
