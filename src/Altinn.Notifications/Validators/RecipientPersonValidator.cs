using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Rules;
using Altinn.Notifications.Validators.Sms;
using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Represents validation logic for the recipient person model.
/// </summary>
public sealed class RecipientPersonValidator : AbstractValidator<RecipientPersonExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientPersonValidator"/> class.
    /// </summary>
    public RecipientPersonValidator() 
    {
        When(recipient => recipient != null, () =>
        {
            RuleFor(recipient => recipient!.NationalIdentityNumber)
                .NotEmpty()
                .MustBeValidNationalIdentityNumber();
        });

        RuleFor(recipient => recipient!.ChannelScheme)
            .IsInEnum()
            .WithMessage("Invalid channel scheme value.");

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
    }
}
