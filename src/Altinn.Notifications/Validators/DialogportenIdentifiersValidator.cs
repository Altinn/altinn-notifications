using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the dialogporten reference request.
    /// </summary>
    public class DialogportenIdentifiersValidator : AbstractValidator<DialogportenIdentifiersExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DialogportenIdentifiersValidator"/> class.
        /// </summary>
        public DialogportenIdentifiersValidator()
        {
            RuleFor(association => association)
                .NotNull()
                .WithMessage("Association cannot be null.");
        }
    }
}
