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
        public EmailSendingOptionsValidator()
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

                When(option => option!.SendingTimePolicy != null, () =>
                {
                    RuleFor(option => option!.SendingTimePolicy!.Value)
                        .Must(IsDaytimeOrAnytime)
                        .WithMessage("Email only supports send time daytime and anytime");
                });

                RuleFor(option => option!.ContentType)
                    .IsInEnum()
                    .WithMessage("Email content type must be either Plain or HTML.");
            });
        }

        private static bool IsDaytimeOrAnytime(SendingTimePolicyExt sendingTime)
        {
            return sendingTime switch
            {
                SendingTimePolicyExt.Daytime => true,
                SendingTimePolicyExt.Anytime => true,
                _ => false
            };
        }
    }
}
