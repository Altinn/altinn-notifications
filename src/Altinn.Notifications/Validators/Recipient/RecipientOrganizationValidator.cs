using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Extensions;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient
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

                RuleFor(options => options!.ChannelSchema)
                    .NotEmpty()
                    .IsInEnum()
                    .WithMessage("ChannelSchema must be a valid value from the NotificationChannelExt enum.");

                When(options => options!.ChannelSchema.IsPreferredSchema(), () =>
                {
                    RuleFor(options => options!.EmailSettings)
                        .NotNull()
                        .WithMessage("EmailSettings must be set when ChannelSchema is SmsPreffered or EmailPreferred");

                    RuleFor(options => options!.SmsSettings)
                        .NotNull()
                        .WithMessage("SmsSettings must be set when ChannelSchema is SmsPreffered or EmailPreferred"); 
                });

                When(options => options!.ChannelSchema == NotificationChannelExt.Sms, () =>
                {
                    RuleFor(options => options!.SmsSettings)
                        .NotNull()
                        .WithMessage("SmsSettings must be set when ChannelSchema is Sms");
                });

                When(options => options!.ChannelSchema == NotificationChannelExt.Email, () =>
                {
                    RuleFor(options => options!.EmailSettings)
                        .NotNull()
                        .WithMessage("EmailSettings must be set when ChannelSchema is Email");
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
