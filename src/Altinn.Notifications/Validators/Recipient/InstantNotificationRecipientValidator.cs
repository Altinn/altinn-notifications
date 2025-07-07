using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Represents validation rules for a timed SMS that should be to a single recipient.
/// </summary>
internal sealed class InstantNotificationRecipientValidator : AbstractValidator<InstantNotificationRecipientExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantNotificationRecipientValidator"/> class.
    /// </summary>
    public InstantNotificationRecipientValidator()
    {
        When(options => options != null, () =>
        {
            RuleFor(recipient => recipient!.RecipientTimedSms)
                .NotNull()
                .WithMessage("SMS recipient information cannot be null.")
                .SetValidator(new RecipientTimedSmsValidator());
        });
    }
}
