using System.Security.Claims;

using AltinnCore.Authentication.Constants;

namespace Altinn.Notifications.Extensions;

/// <summary>
/// Extensions for claimsprincial
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Get the org identifier string or null if it is not an org.
    /// </summary>        
    public static string? GetOrg(this ClaimsPrincipal user)
    {
        if (user.HasClaim(c => c.Type == AltinnCoreClaimTypes.Org))
        {
            Claim? orgClaim = user.FindFirst(c => c.Type == AltinnCoreClaimTypes.Org);
            if (orgClaim != null)
            {
                return orgClaim.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a boolean indicating if the provided required scope is present in the scope claim.
    /// </summary>
    public static bool HasRequiredScope(this ClaimsPrincipal user, string requiredScope)
    {
        string? contextScope = user.Identities
          ?.FirstOrDefault(i => i.AuthenticationType != null && i.AuthenticationType.Equals("AuthenticationTypes.Federation"))?.Claims
          .Where(c => c.Type.Equals("urn:altinn:scope"))?
          .Select(c => c.Value).FirstOrDefault();

        contextScope ??= user.Claims.Where(c => c.Type.Equals("scope")).Select(c => c.Value).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(contextScope) && contextScope.Contains(requiredScope, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }
}