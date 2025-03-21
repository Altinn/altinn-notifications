using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
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
        }
    }
}
