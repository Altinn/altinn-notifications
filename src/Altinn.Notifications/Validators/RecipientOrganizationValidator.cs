using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Validator for the <see cref="RecipientOrganizationExt"/> model.
    /// </summary>
    public class RecipientOrganizationValidator : AbstractValidator<RecipientOrganizationExt?>
    {
        private readonly int _organizationNumberLength = 9;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientOrganizationValidator"/> class.
        /// </summary>
        public RecipientOrganizationValidator() 
        {
            When(options => options != null, () =>
            {
                RuleFor(options => options!.OrgNumber)
                    .NotNull()
                    .NotEmpty()
                    .WithMessage("OrgNumber cannot be null or empty.");

                RuleFor(options => options!.OrgNumber)
                    .Must(on => on?.Length == _organizationNumberLength && on.All(char.IsDigit))
                    .When(options => !string.IsNullOrEmpty(options!.OrgNumber))
                    .WithMessage($"Organization number must be {_organizationNumberLength} digits long.");

                RuleFor(options => options!.ChannelScheme)
                    .NotEmpty()
                    .IsInEnum()
                    .WithMessage("ChannelScheme must be a valid value from the NotificationChannelExt enum.");

                When(options => options!.ChannelScheme == NotificationChannelExt.SmsPreferred || options!.ChannelScheme == NotificationChannelExt.EmailPreferred, () =>
                {
                    RuleFor(options => options!.EmailSettings)
                        .NotNull()
                        .WithMessage("EmailSettings must be set when ChannelScheme is SmsPreffered or EmailPreferred");

                    RuleFor(options => options!.SmsSettings)
                        .NotNull()
                        .WithMessage("SmsSettings must be set when ChannelScheme is SmsPreffered or EmailPreferred"); 
                });

                When(options => options!.ChannelScheme == NotificationChannelExt.Sms, () =>
                {
                    RuleFor(options => options!.SmsSettings)
                        .NotNull()
                        .WithMessage("SmsSettings must be set when ChannelScheme is Sms");
                });

                When(options => options!.ChannelScheme == NotificationChannelExt.Email, () =>
                {
                    RuleFor(options => options!.EmailSettings)
                        .NotNull()
                        .WithMessage("EmailSettings must be set when ChannelScheme is Email");
                });

                When(options => options!.EmailSettings != null, () =>
                {
                    RuleFor(options => options!.EmailSettings)
                        .SetValidator(new EmailSendingOptionsValidator());
                });

                When(options => options!.SmsSettings != null, () =>
                {
                    RuleFor(options => options!.SmsSettings)
                        .SetValidator(new SmsSendingOptionsValidator());
                });
            });
        }
    }
}
