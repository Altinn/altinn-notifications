using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Recipient;

/// <summary>
/// Represents validation logic for the self-identified user recipient model.
/// </summary>
internal sealed partial class RecipientSelfIdentifiedUserValidator : AbstractValidator<RecipientSelfIdentifiedUserExt?>
{
    /// <summary>
    /// The expected prefix for self-identified user external identity URN.
    /// </summary>
    private const string _externalIdentityPrefix = "urn:altinn:person:idporten-email:";

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientSelfIdentifiedUserValidator"/> class.
    /// </summary>
    public RecipientSelfIdentifiedUserValidator()
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
                .WithMessage($"ExternalIdentity must be in the format '{_externalIdentityPrefix}{{email-address}}' with a valid email address.");

            RuleFor(options => options!.ResourceId)
                .Must(resourceId => RecipientRules.BeValidResourceId(resourceId!))
                .When(options => options!.ResourceId != null)
                .WithMessage("ResourceId must have a valid syntax.");
        });
    }

    /// <summary>
    /// Validates that the external identity is in the correct URN format with a valid email.
    /// </summary>
    /// <param name="externalIdentity">The external identity to validate.</param>
    /// <returns>True if the external identity is valid; otherwise, false.</returns>
    private static bool BeValidExternalIdentity(string externalIdentity)
    {
        if (string.IsNullOrWhiteSpace(externalIdentity))
        {
            return false;
        }

        if (!externalIdentity.StartsWith(_externalIdentityPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var email = externalIdentity[_externalIdentityPrefix.Length..];

        return RecipientRules.IsValidEmail(email);
    }
}
