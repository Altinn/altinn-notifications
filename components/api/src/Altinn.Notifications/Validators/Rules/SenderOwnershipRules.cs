using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using FluentValidation;

namespace Altinn.Notifications.Validators.Rules;

/// <summary>
/// Provides validation rules ensuring that the senders of a request are allowed for the ordering service owner.
/// The rules are only applied when the service owner has been added to the validation context by
/// <c>ValidatorExtensions.Validate</c>.
/// </summary>
public static class SenderOwnershipRules
{
    /// <summary>
    /// The key of the short name of the ordering service owner in the root context data of the validation context.
    /// </summary>
    public const string ServiceOwnerContextKey = "SenderOwnershipServiceOwner";

    /// <summary>
    /// The key of the <see cref="IServiceOwnerService"/> in the root context data of the validation context.
    /// </summary>
    public const string ServiceOwnerServiceContextKey = "SenderOwnershipServiceOwnerService";

    /// <summary>
    /// Validates that the email sender is allowed for the ordering service owner.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <typeparam name="TProperty">The type of the sender property.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the sender property.</param>
    /// <returns>The rule builder options.</returns>
    public static IRuleBuilderOptions<T, TProperty> MustBeAllowedEmailSender<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
    {
        return ruleBuilder
            .Must((_, sender, context) => IsAllowed(context.RootContextData, AddressType.Email, sender))
            .WithMessage("The sender email address is not registered for the service owner.");
    }

    /// <summary>
    /// Validates that the SMS sender is allowed for the ordering service owner.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <typeparam name="TProperty">The type of the sender property.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the sender property.</param>
    /// <returns>The rule builder options.</returns>
    public static IRuleBuilderOptions<T, TProperty> MustBeAllowedSmsSender<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
    {
        return ruleBuilder
            .Must((_, sender, context) => IsAllowed(context.RootContextData, AddressType.Sms, sender))
            .WithMessage("The SMS sender is not registered for the service owner.");
    }

    /// <summary>
    /// Checks whether the sender is allowed for the ordering service owner, using the service owner stored under
    /// <see cref="ServiceOwnerContextKey"/> and the <see cref="IServiceOwnerService"/> stored under
    /// <see cref="ServiceOwnerServiceContextKey"/> in the root context data of the validation context.
    /// </summary>
    /// <remarks>
    /// If no service owner is stored, the sender is allowed and the result is <see langword="true"/>.
    /// This happens when validation runs without the sender ownership check.
    /// Before the sender is checked, it is converted to a <see cref="string"/> and trimmed, matching how the mappers store it.
    /// If the sender is not a <see cref="string"/>, <see langword="null"/> is checked.
    /// </remarks>
    /// <typeparam name="TProperty">The type of the sender property being validated.</typeparam>
    /// <param name="contextData">The root context data of the validation context, which may contain the service owner.</param>
    /// <param name="addressType">The type of sender being checked, either email or SMS.</param>
    /// <param name="sender">The sender value to check, such as an email address or SMS sender.</param>
    /// <returns>
    /// <see langword="true"/> if no service owner is stored or the sender is allowed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the service owner is not a <see cref="string"/>, or if no <see cref="IServiceOwnerService"/> is stored.
    /// </exception>
    private static bool IsAllowed<TProperty>(IDictionary<string, object> contextData, AddressType addressType, TProperty sender)
    {
        if (!contextData.TryGetValue(ServiceOwnerContextKey, out var serviceOwner))
        {
            return true;
        }

        if (serviceOwner is not string owner
            || !contextData.TryGetValue(ServiceOwnerServiceContextKey, out var service)
            || service is not IServiceOwnerService serviceOwnerService)
        {
            throw new InvalidOperationException("The sender ownership check is not set up correctly in the validation context.");
        }

        return serviceOwnerService.IsSenderAllowed(owner, addressType, (sender as string)?.Trim());
    }
}
