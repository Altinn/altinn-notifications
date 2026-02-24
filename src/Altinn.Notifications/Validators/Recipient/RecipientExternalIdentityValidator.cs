using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Represents validation logic for the external identity recipient model.
/// </summary>
internal sealed partial class RecipientExternalIdentityValidator : AbstractValidator<RecipientExternalIdentityExt?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientExternalIdentityValidator"/> class.
    /// </summary>
    public RecipientExternalIdentityValidator()
    {
        Include(new RecipientBaseValidator());

        When(options => options != null, () =>
        {
            RuleFor(options => options!.ExternalIdentity)
                .NotEmpty()
                .WithMessage("ExternalIdentity cannot be null or empty.");

            RuleFor(options => options!.ExternalIdentity)
                .Must(BeValidExternalIdentity)
                .When(options => !string.IsNullOrEmpty(options!.ExternalIdentity))
                .WithMessage("Invalid external identity URN format.");

            RuleFor(options => options!.ResourceId)
                .Must(resourceId => RecipientRules.BeValidResourceId(resourceId!))
                .When(options => options!.ResourceId != null)
                .WithMessage("ResourceId must have a valid syntax.");
        });
    }

    /// <summary>
    /// Validates that the external identity is in a valid URN format using <see cref="ExternalIdentityUrn"/>.
    /// </summary>
    /// <param name="externalIdentity">The external identity to validate.</param>
    /// <returns>True if the external identity is valid; otherwise, false.</returns>
    private static bool BeValidExternalIdentity(string externalIdentity)
    {
        if (string.IsNullOrWhiteSpace(externalIdentity))
        {
            return false;
        }

        if (!ExternalIdentityUrn.TryParse(externalIdentity, out var urn))
        {
            return false;
        }

        if (urn is ExternalIdentityUrn.IDPortenEmail idportenEmail)
        {
            return RecipientRules.IsValidEmail(idportenEmail.Value.Value);
        }

        if (urn is ExternalIdentityUrn.Username username)
        {
            return !string.IsNullOrWhiteSpace(username.Value.Value);
        }

        return false;
    }
}
