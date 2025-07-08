using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Represents validation rules for recipients of scheduled SMS messages.
/// </summary>
internal sealed class RecipientTimedSmsValidator : AbstractValidator<RecipientTimedSmsExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientTimedSmsValidator"/> class.
    /// </summary>
    public RecipientTimedSmsValidator()
    {
        RuleFor(recipient => recipient)
            .NotNull()
            .WithMessage("SMS recipient cannot be null.")
            .DependentRules(() =>
            {
                RuleFor(recipient => recipient.PhoneNumber)
                    .NotEmpty()
                    .WithMessage("Recipient phone number cannot be null or empty.");

                RuleFor(recipient => recipient.PhoneNumber)
                    .Must(MobileNumberHelper.IsValidMobileNumber)
                    .WithMessage("Recipient phone number is not a valid mobile number.");

                RuleFor(recipient => recipient.TimeToLiveInSeconds)
                    .InclusiveBetween(60, 172800)
                    .WithMessage("Time-to-live must be between 60 and 172800 seconds (48 hours).");

                RuleFor(recipient => recipient.Details)
                    .NotNull()
                    .WithMessage("SMS details cannot be null.")
                    .SetValidator(new SmsDetailsValidator());
            });
    }
}
