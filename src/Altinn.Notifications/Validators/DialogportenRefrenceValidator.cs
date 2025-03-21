using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the dialogporten reference request.
    /// </summary>
    public class DialogportenRefrenceValidator : AbstractValidator<DialogportenReferenceExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DialogportenRefrenceValidator"/> class.
        /// </summary>
        public DialogportenRefrenceValidator()
        {
            RuleFor(association => association)
                .NotNull()
                .WithMessage("Association cannot be null.");
        }
    }
}
