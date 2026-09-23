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
}
