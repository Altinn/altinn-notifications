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
        
        When(recipient => recipient != null, () =>
        {
            RuleFor(recipient => recipient!.NationalIdentityNumber)
                .NotEmpty()
                .MustBeValidNationalIdentityNumber();
        });
    }
}
