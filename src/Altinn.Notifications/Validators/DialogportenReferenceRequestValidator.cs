using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the dialogporten reference request.
    /// </summary>
    public class DialogportenReferenceRequestValidator : AbstractValidator<DialogportenReferenceRequestExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DialogportenReferenceRequestValidator"/> class.
        /// </summary>
        public DialogportenReferenceRequestValidator()
        {
            RuleFor(association => association)
                .NotNull()
                .WithMessage("Association cannot be null.");
        }
    }
}
