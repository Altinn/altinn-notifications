using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the dialogporten association model.
    /// </summary>
    public class DialogportenAssociationValidator : AbstractValidator<DialogportenAssociationExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DialogportenAssociationValidator"/> class.
        /// </summary>
        public DialogportenAssociationValidator()
        {
            RuleFor(association => association)
                .NotNull()
                .WithMessage("Association cannot be null.");
        }
    }
}
