using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Rules;
using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient
{
    /// <summary>
    /// Validator for the <see cref="RecipientOrganizationExt"/> model.
    /// </summary>
    internal sealed class RecipientOrganizationValidator : AbstractValidator<RecipientOrganizationExt?>
    {
        private readonly int _organizationNumberLength = 9;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientOrganizationValidator"/> class.
        /// </summary>
        public RecipientOrganizationValidator()
        {
            Include(new RecipientBaseValidator());

            When(options => options != null, () =>
            {
                RuleFor(options => options!.OrgNumber)
                    .NotNull()
                    .NotEmpty()
                    .WithMessage("OrgNumber cannot be null or empty.");

                RuleFor(options => options!.OrgNumber)
                    .Must(on => on?.Length == _organizationNumberLength && on.All(char.IsDigit))
                    .When(options => !string.IsNullOrEmpty(options!.OrgNumber))
                    .WithMessage($"Organization number must be {_organizationNumberLength} digits long.");

                RuleFor(options => options!.ResourceId)
                    .Must(arg => RecipientRules.BeValidResourceId(arg!))
                    .When(options => options!.ResourceId != null)
                    .WithMessage("ResourceId must have a valid syntax.");
            });
        }
    }
}
