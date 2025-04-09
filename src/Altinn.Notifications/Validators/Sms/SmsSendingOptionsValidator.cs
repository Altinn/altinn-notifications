using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms
{
    /// <summary>
    /// Represents validation logic for the SMS sending options model.
    /// </summary>
    internal sealed class SmsSendingOptionsValidator : AbstractValidator<SmsSendingOptionsExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmsSendingOptionsValidator"/> class.
        /// </summary>
        public SmsSendingOptionsValidator()
        {
            When(option => option != null, () =>
            {
                RuleFor(option => option!.Body)
                    .NotNull()
                    .NotEmpty()
                    .WithMessage("SMS body cannot be null or empty.");
                
                RuleFor(option => option!.SendingTimePolicy)
                    .Must(HaveValueDaytimeOrAnytime)
                    .WithMessage("SMS only supports send time daytime and anytime");
            });
        }

        private static bool HaveValueDaytimeOrAnytime(SendingTimePolicyExt sendingTime)
        {
            return sendingTime == SendingTimePolicyExt.Daytime || sendingTime == SendingTimePolicyExt.Anytime;
        }
    }
}
