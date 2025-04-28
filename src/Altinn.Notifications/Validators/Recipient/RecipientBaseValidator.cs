using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Extensions;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Represents validation logic for the recipient base model.
/// </summary>
public class RecipientBaseValidator : AbstractValidator<RecipientBaseExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientBaseValidator"/> class.
    /// </summary>
    public RecipientBaseValidator()
    {
        RuleFor(recipient => recipient!.ChannelSchema)
      .IsInEnum()
      .WithMessage("Invalid channel scheme value.");

        When(options => options!.ChannelSchema.IsDualChannelSchema(), () =>
        {
            RuleFor(options => options!.EmailSettings)
                .NotNull()
                .WithMessage("EmailSettings must be set when ChannelSchema is EmailAndSms");

            RuleFor(options => options!.SmsSettings)
                .NotNull()
                .WithMessage("SmsSettings must be set when ChannelSchema is EmailAndSms");
        });

        When(options => options!.ChannelSchema.IsFallbackChannelSchema(), () =>
        {
            RuleFor(options => options!.EmailSettings)
                .NotNull()
                .WithMessage("EmailSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred");

            RuleFor(options => options!.SmsSettings)
                .NotNull()
                .WithMessage("SmsSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred");
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
