using Altinn.Notifications.Core.Models;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Extensions;

/// <summary>
/// Extension methods for the <see cref="ServiceError"/> class. Primary use is to support logic that needs
/// to extract information into a different model.
/// </summary>
public static class ServiceErrorExtensions
{
    /// <summary>
    /// Adds the error collection of a <see cref="ServiceError"/> into the given <see cref="ModelStateDictionary"/>.
    /// </summary>
    public static void AddToModelState(this ServiceError serviceError, ModelStateDictionary modelState)
    {
        foreach (var error in serviceError.Errors)
        {
            modelState.AddModelError(error.Key, error.Value);
        }
    }
}
