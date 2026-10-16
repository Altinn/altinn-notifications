using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Email
{
    /// <summary>
    /// Represents validation logic for the email sending options.
    /// </summary>
    internal sealed class EmailSendingOptionsValidator : AbstractValidator<EmailSendingOptionsExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EmailSendingOptionsValidator"/> class.
        /// </summary>
        /// <param name="allowDaytime">
        /// Whether the <see cref="SendingTimePolicyExt.Daytime"/> sending time policy is accepted.
        /// Set to <c>false</c> for email flows that do not support it, such as composed emails.
        /// </param>
        public EmailSendingOptionsValidator(bool allowDaytime = true)
        {
            RuleFor(options => options)
                .NotNull()
                .WithMessage("Email sending options cannot be null.");

            When(options => options != null, () =>
            {
                When(options => options!.SenderEmailAddress != null, () =>
                {
                    RuleFor(options => options!.SenderEmailAddress)
                        .Must(RecipientRules.IsValidEmail)
                        .WithMessage("The sender email address is not valid.");
                });

                RuleFor(options => options!.Subject)
                    .NotEmpty()
                    .WithMessage("The email subject must not be empty.");

                RuleFor(options => options!.Body)
                    .NotEmpty()
                    .WithMessage("The email body must not be empty.");

                if (allowDaytime)
                {
                    RuleFor(option => option!.SendingTimePolicy)
                        .Must(HaveValueDaytimeOrAnytime)
                        .WithMessage("Email only supports send time daytime and anytime");
                }
                else
                {
                    RuleFor(option => option!.SendingTimePolicy)
                        .Must(HaveValueAnytime)
                        .WithMessage("Email only supports send time anytime");
                }

                RuleFor(option => option!.ContentType)
                    .IsInEnum()
                    .WithMessage("Email content type must be either Plain or HTML.");
            });
        }

        private static bool HaveValueDaytimeOrAnytime(SendingTimePolicyExt sendingTime)
        {
            return sendingTime is SendingTimePolicyExt.Daytime or SendingTimePolicyExt.Anytime;
        }

        private static bool HaveValueAnytime(SendingTimePolicyExt sendingTime)
        {
            return sendingTime == SendingTimePolicyExt.Anytime;
        }
    }
}
