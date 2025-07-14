using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Represents validation rules for recipients of notification orders intended for immediate delivery.
/// </summary>
internal sealed class InstantNotificationRecipientValidator : AbstractValidator<InstantNotificationRecipientExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantNotificationRecipientValidator"/> class.
    /// </summary>
    public InstantNotificationRecipientValidator()
    {
        RuleFor(recipient => recipient)
            .NotNull()
            .WithMessage("Notification recipient cannot be null.")
            .DependentRules(() =>
            {
                RuleFor(recipient => recipient.ShortMessageDeliveryDetails)
                    .NotNull()
                    .WithMessage("SMS delivery details cannot be null.")
                    .SetValidator(new ShortMessageDeliveryDetailsValidator());
            });
    }
}
