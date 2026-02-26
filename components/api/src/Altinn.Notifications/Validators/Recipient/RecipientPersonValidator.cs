using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Represents validation logic for the recipient person model.
/// </summary>
internal sealed class RecipientPersonValidator : AbstractValidator<RecipientPersonExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientPersonValidator"/> class.
    /// </summary>
    public RecipientPersonValidator()
    {
        Include(new RecipientBaseValidator());

        When(options => options != null, () =>
        {
            RuleFor(options => options!.NationalIdentityNumber)
                .NotEmpty()
                .MustBeValidNationalIdentityNumber();

            RuleFor(options => options!.ResourceId)
                .Must(arg => RecipientRules.BeValidResourceId(arg!))
                .When(options => options!.ResourceId != null)
                .WithMessage("ResourceId must have a valid syntax.");
        });
    }
}
