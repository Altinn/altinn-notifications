using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Recipient;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the recipient types associated with request model.
    /// </summary>
    internal sealed class NotificationRecipientValidator : AbstractValidator<NotificationRecipientExt>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationRecipientValidator"/> class.
        /// </summary>
        public NotificationRecipientValidator()
        {
            RuleFor(specification => specification)
                .Must(HaveOneSetRecipientOnly)
                .WithMessage("Must have exactly one recipient.");

            RuleFor(specification => specification)
                .NotNull()
                .WithMessage("Recipient specification cannot be null.");

            RuleFor(specification => specification.RecipientEmail)
               .SetValidator(validator: new RecipientEmailValidator());

            RuleFor(specification => specification.RecipientSms)
                .SetValidator(validator: new RecipientSmsValidator());

            RuleFor(specification => specification.RecipientPerson)
                .SetValidator(validator: new RecipientPersonValidator());

            RuleFor(specification => specification.RecipientOrganization)
                .SetValidator(validator: new RecipientOrganizationValidator());
        }

        /// <summary>
        /// Checks if only one recipient is set.
        /// </summary>
        /// <param name="specification">Object containing four possible recipient types</param>
        /// <returns></returns>
        private static bool HaveOneSetRecipientOnly(NotificationRecipientExt specification)
        {
            return new object?[]
            {
                specification.RecipientEmail,
                specification.RecipientSms,
                specification.RecipientPerson,
                specification.RecipientOrganization
            }.Count(recipient => recipient != null) == 1;
        }
    }
}
