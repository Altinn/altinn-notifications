using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="NotificationOrderChainRequestExt"/> model
/// </summary>
public class NotificationOrderChainRequestValidator : AbstractValidator<NotificationOrderChainRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderChainRequestValidator"/> class.
    /// </summary>
    public NotificationOrderChainRequestValidator()
    {
        RuleFor(order => order.IdempotencyId) // todo: check type
            .NotNull()
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        // required
        RuleFor(order => order.Recipient)
            .NotNull()
            .SetValidator(validator: new NotificationRecipientValidator());

        // should not run if DialogportenAssociation is null
        When(order => order.DialogportenAssociation != null, () =>
        {
            RuleFor(order => order.DialogportenAssociation)
                .SetValidator(validator: new DialogportenIdentifiersValidator());
        });

        When(order => order.Reminders != null, () =>
        {
            RuleForEach(order => order.Reminders)
                .SetValidator(validator: new NotificationReminderValidator());
        });
    }
}
