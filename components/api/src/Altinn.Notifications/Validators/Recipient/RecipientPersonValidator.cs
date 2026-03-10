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

            RuleFor(options => options!.ResourceAction)
                .Must((recipient, resourceAction) =>
                {
                    if (string.IsNullOrWhiteSpace(recipient!.ResourceId))
                    {
                        return string.IsNullOrEmpty(resourceAction);
                    }

                    return string.IsNullOrEmpty(resourceAction) || !string.IsNullOrWhiteSpace(resourceAction);
                })
                .WithMessage((recipient, _) =>
                    string.IsNullOrWhiteSpace(recipient!.ResourceId)
                        ? "ResourceAction cannot be specified without a ResourceId."
                        : "ResourceAction cannot be blank or whitespace.");
        });
    }
}
