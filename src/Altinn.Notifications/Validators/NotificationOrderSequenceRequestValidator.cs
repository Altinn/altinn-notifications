using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="NotificationOrderSequenceRequestExt"/> model
/// </summary>
public class NotificationOrderSequenceRequestValidator : AbstractValidator<NotificationOrderSequenceRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderSequenceRequestValidator"/> class.
    /// </summary>
    public NotificationOrderSequenceRequestValidator()
    {
        RuleFor(order => order.IdempotencyId) // todo: check type
            .NotNull()
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        // required
        RuleFor(order => order.Recipient)
            .NotNull()
            .SetValidator(validator: new RecipientSpecificationValidator());

        // should not run if DialogportenAssociation is null
        When(order => order.DialogportenAssociation != null, () =>
        {
            RuleFor(order => order.DialogportenAssociation)
                .SetValidator(validator: new DialogportenRefrenceValidator());
        });

        When(order => order.Reminders != null, () =>
        {
            RuleForEach(order => order.Reminders)
                .SetValidator(validator: new NotificationReminderValidator());
        });
    }
}
