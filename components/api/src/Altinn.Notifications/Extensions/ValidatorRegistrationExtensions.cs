using System.Reflection;
using FluentValidation;

namespace Altinn.Notifications.Extensions;

/// <summary>
/// Extension methods for registering FluentValidation validators and checking for duplicates.
/// </summary>
public static class ValidatorRegistrationExtensions
{
    /// <summary>
    /// Registers all validators from the specified assembly and checks for duplicate validators.
    /// </summary>
    /// <param name="services">The serviceCollection</param>
    /// <param name="assembly">The assembly that should be checked</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Exception thrown if there are duplicate validators registered in the assembly</exception>
    public static IServiceCollection AddValidatorsFromAssemblyWithDuplicateCheck(this IServiceCollection services, Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Check for duplicate validators
        var validatorTypes = services
            .Where(sd => typeof(IValidator).IsAssignableFrom(sd.ServiceType) && !sd.ServiceType.IsGenericType)
            .Select(sd => sd.ServiceType)
            .ToList();

        var duplicateValidators = validatorTypes
            .GroupBy(vt => vt.BaseType!.GenericTypeArguments.FirstOrDefault())
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateValidators.Count > 0)
        {
            var duplicateTypeNames = string.Join(", ", duplicateValidators.Select(g => g.Key!.Name));
            throw new InvalidOperationException($"Duplicate validators found for types: {duplicateTypeNames}");
        }

        return services;
    }
}
