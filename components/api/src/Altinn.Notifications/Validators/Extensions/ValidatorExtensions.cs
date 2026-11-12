using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;
using FluentValidation.Results;

namespace Altinn.Notifications.Validators.Extensions;

/// <summary>
/// Extension class for <see cref="IValidator{T}"/>
/// </summary>
public static class ValidatorExtensions
{
    /// <summary>
    /// Validates the request, including that its senders are allowed for the service owner.
    /// The sender rules are skipped if no service owner is given.
    /// </summary>
    /// <typeparam name="T">The type of the request.</typeparam>
    /// <param name="validator">The validator of the request.</param>
    /// <param name="instance">The request to validate.</param>
    /// <param name="serviceOwner">The short name of the service owner placing the order.</param>
    /// <param name="serviceOwnerService">The service used to check the senders of the service owner.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult Validate<T>(this IValidator<T> validator, T instance, string? serviceOwner, IServiceOwnerService serviceOwnerService)
    {
        var context = new ValidationContext<T>(instance);

        if (!string.IsNullOrWhiteSpace(serviceOwner))
        {
            context.RootContextData[SenderOwnershipRules.ServiceOwnerContextKey] = serviceOwner;
            context.RootContextData[SenderOwnershipRules.ServiceOwnerServiceContextKey] = serviceOwnerService;
        }

        return validator.Validate(context);
    }
}
