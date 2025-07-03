using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Represents validation rules for the model responsible for immediate SMS delivery to a single recipient.
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

            RuleFor(recipient => recipient!.TimeToLiveInSeconds)
                .InclusiveBetween(1, 172800)
                .WithMessage("Time-to-live must be between 1 and 172800 seconds (48 hours).");

            RuleFor(recipient => recipient!.Details)
                .NotNull()
                .WithMessage("SMS details cannot be null.");

            RuleFor(recipient => recipient!.Details.Body)
                .NotEmpty()
                .WithMessage("SMS message body cannot be null or empty.");
        });
    }
}
