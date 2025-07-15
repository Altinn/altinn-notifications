using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Represents validation logic for delivery details for an SMS including recipient, content, and delivery parameters.
/// </summary>
internal sealed class ShortMessageDeliveryDetailsValidator : AbstractValidator<ShortMessageDeliveryDetailsExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShortMessageDeliveryDetailsValidator"/> class.
    /// </summary>
    public ShortMessageDeliveryDetailsValidator()
    {
        RuleFor(recipient => recipient.PhoneNumber)
            .Must(MobileNumberHelper.IsValidMobileNumber)
            .WithMessage("Recipient phone number is not a valid mobile number.");

        RuleFor(recipient => recipient.TimeToLiveInSeconds)
            .InclusiveBetween(60, 172800)
            .WithMessage("Time-to-live must be between 60 and 172800 seconds (48 hours).");
    }
}
