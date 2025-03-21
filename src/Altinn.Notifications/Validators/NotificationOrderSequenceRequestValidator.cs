using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators
{
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
            // should not run if DialogportenAssociation is null
            When(order => order.DialogportenAssociation != null, () =>
            {
                RuleFor(order => order.DialogportenAssociation)
                    .SetValidator(validator: new DialogportenReferenceRequestValidator());
            });

            RuleFor(order => order.DialogportenAssociation)
                .SetValidator(validator: new DialogportenReferenceRequestValidator());

            RuleFor(order => order.Recipient)
                .SetValidator(validator: new RecipientSpecificationRequestValidator());

            RuleForEach(order => order.Reminders)
                .SetValidator(validator: new NotificationReminderRequestValidator());
        }
    }
}
