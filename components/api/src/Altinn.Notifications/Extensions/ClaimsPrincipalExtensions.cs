#nullable enable

using System.Security.Claims;

using AltinnCore.Authentication.Constants;

namespace Altinn.Notifications.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> instances.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Retrieves the application owner short name from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The application owner short name if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetOrg(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.Org);
    }

    /// <summary>
    /// Retrieves the user identifier from the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <returns>The user identifier if the claim exists; otherwise, <c>null</c>.</returns>
    public static string? GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(AltinnCoreClaimTypes.UserId);
    }

    /// <summary>
    /// Determines whether the specified required scope is present in the user's claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> instance representing the user.</param>
    /// <param name="requiredScope">The required scope to check for.</param>
    /// <returns><c>true</c> if the required scope is present; otherwise, <c>false</c>.</returns>
    public static bool HasRequiredScope(this ClaimsPrincipal user, string requiredScope)
    {
        var contextScopes = user.Identities?
            .FirstOrDefault(e => e.AuthenticationType != null && e.AuthenticationType.Equals("AuthenticationTypes.Federation"))?
            .Claims
            .Where(e => e.Type.Equals("urn:altinn:scope"))
            .Select(e => e.Value)
            .FirstOrDefault()?
            .Split(' ');

        contextScopes ??= user.Claims
            .Where(e => e.Type.Equals("scope"))
            .Select(e => e.Value)
            .FirstOrDefault()?
            .Split(' ');

        return contextScopes != null && contextScopes.Any(x => x.Equals(requiredScope, StringComparison.InvariantCultureIgnoreCase));
    }
}
