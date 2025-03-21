using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators.Sms
{
    /// <summary>
    /// Represents validation logic for the SMS sending options model.
    /// </summary>
    public sealed class SmsSendingOptionsValidator : AbstractValidator<SmsSendingOptionsExt>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmsSendingOptionsValidator"/> class.
        /// </summary>
        public SmsSendingOptionsValidator()
        {
            RuleFor(option => option.Body)
                .NotNull()
                .NotEmpty()
                .WithMessage("SMS sending options cannot be null or empty.");

            RuleFor(option => option.Sender)
                .NotNull()
                .NotEmpty()
                .WithMessage("SMS sender cannot be null or empty.");
        }
    }
}
