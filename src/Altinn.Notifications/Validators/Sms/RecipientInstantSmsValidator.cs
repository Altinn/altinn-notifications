using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Represents validation rules for a recipient of an instant SMS.
/// </summary>
internal sealed class RecipientInstantSmsValidator : AbstractValidator<RecipientInstantSmsExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientInstantSmsValidator"/> class.
    /// </summary>
    public RecipientInstantSmsValidator()
    {
        When(options => options != null, () =>
        {
            RuleFor(recipient => recipient!.PhoneNumber)
                .NotEmpty()
                .WithMessage("Recipient phone number cannot be null or empty.");

            RuleFor(recipient => recipient!.PhoneNumber)
                .Must(MobileNumberHelper.IsValidMobileNumber)
                .WithMessage("Recipient phone number is not a valid mobile number.");

            RuleFor(sms => sms!.TimeToLiveInSeconds)
                .InclusiveBetween(60, 172800)
                .WithMessage("Time-to-live must be between 60 and 172800 seconds (48 hours).");

            RuleFor(sms => sms!.Details)
                .NotNull()
                .SetValidator(new SmsDetailsValidator());
        });
    }
}
