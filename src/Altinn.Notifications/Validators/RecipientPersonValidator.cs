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
        RuleFor(recipient => recipient)
            .NotNull()
            .WithMessage("Recipient person object cannot be null.");

        When(recipient => recipient != null, () =>
        {
            RuleFor(recipient => recipient!.NationalIdentityNumber)
                .NotEmpty()
                .MustBeValidNationalIdentityNumber();
        });

        RuleFor(recipient => recipient!.ChannelScheme)
            .IsInEnum()
            .WithMessage("Invalid channel scheme value.");

        RuleFor(recipient => recipient!.EmailSettings)
            .SetValidator(new EmailSendingOptionsValidator());

        RuleFor(recipient => recipient!.SmsSettings)
            .SetValidator(new SmsSendingOptionsValidator());
    }
}
