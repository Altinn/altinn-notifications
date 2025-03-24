using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators.Sms
{
    /// <summary>
    /// Represents validation logic for the SMS recipient model.
    /// </summary>
    internal sealed class RecipientSmsValidator : AbstractValidator<RecipientSmsExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientSmsValidator"/> class.
        /// </summary>
        public RecipientSmsValidator()
        {
            When(options => options != null, () =>
            {
                RuleFor(recipient => recipient!.PhoneNumber)
                    .NotNull()
                    .NotEmpty()
                    .WithMessage("Recipient phone number cannot be null, empty, or invalid.");

                RuleFor(recipient => recipient!.PhoneNumber)
                    .Must(MobileNumberHelper.IsValidMobileNumber)
                    .WithMessage("Recipient phone number is not a valid mobile number.");

                RuleFor(recipient => recipient!.Settings)
                    .NotNull()
                    .SetValidator(new SmsSendingOptionsValidator());
            });
        }
    }
}
