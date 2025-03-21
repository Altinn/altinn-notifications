using Altinn.Notifications.Models;
using FluentValidation;
using FluentValidation.Validators;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the recipient person model.
    /// </summary>
    public sealed class RecipientPersonValidator : AbstractValidator<RecipientPersonExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientPersonValidator"/> class.
        /// </summary>
        public RecipientPersonValidator()
        {
        }
    }
}
