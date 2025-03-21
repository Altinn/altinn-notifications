using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Sms;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the recipient types associated with request model.
    /// </summary>
    public class RecipientSpecificationValidator : AbstractValidator<RecipientSpecificationExt>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientSpecificationValidator"/> class.
        /// </summary>
        public RecipientSpecificationValidator()
        {
            RuleFor(specification => specification)
                .NotNull()
                .WithMessage("Recipient specification cannot be null.");

            RuleFor(specification => specification.RecipientEmail)
               .SetValidator(validator: new EmailRecipientValidator());

            RuleFor(specification => specification.RecipientSms)
                .SetValidator(validator: new RecipientSmsValidator());

            RuleFor(specification => specification.RecipientPerson)
                .SetValidator(validator: new RecipientPersonValidator());

            RuleFor(specification => specification.RecipientOrganization)
                .SetValidator(validator: new RecipientOrganizationValidator());
        }
    }
}
