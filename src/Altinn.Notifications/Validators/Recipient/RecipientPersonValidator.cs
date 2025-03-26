using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Extensions;
using Altinn.Notifications.Validators.Rules;
using Altinn.Notifications.Validators.Sms;
using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

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

        RuleFor(recipient => recipient!.ChannelSchema)
            .IsInEnum()
            .WithMessage("Invalid channel scheme value.");

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
    }
}
