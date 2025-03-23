using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Validator for the <see cref="RecipientOrganizationExt"/> model.
    /// </summary>
    public class RecipientOrganizationValidator : AbstractValidator<RecipientOrganizationExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientOrganizationValidator"/> class.
        /// </summary>
        public RecipientOrganizationValidator()
        {
        }
    }
}
