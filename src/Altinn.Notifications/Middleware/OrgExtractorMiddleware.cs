using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using AltinnCore.Authentication.Constants;

namespace Altinn.Notifications.Middleware;

/// <summary>
/// Middleware for extracting org information in an HTTP request
/// from either the issuer of PlatformAccessToken header or as
/// an org claim in the bearer token.
/// </summary>
public class OrgExtractorMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgExtractorMiddleware"/> class.
    /// </summary>
    public OrgExtractorMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Retrieve org claim and save in httpContext as Creator item. 
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldApplyMiddleware(context.Request.Path))
        {
            string? org = GetOrgFromHttpContext(context);

            if (org != null)
            {
                context.Items["Org"] = org;
            }
        }

        await _next(context);
    }

    private static string? GetOrgFromHttpContext(HttpContext context)
    {
        string? accessToken = context.Request.Headers["PlatformAccessToken"];
        if (!string.IsNullOrEmpty(accessToken))
        {
            return GetIssuerOfAccessToken(accessToken);
        }

        return GetOrgFromClaim(context.User);
    }

    private static string GetIssuerOfAccessToken(string accessToken)
    {
        JwtSecurityTokenHandler validator = new();
        JwtSecurityToken jwt = validator.ReadJwtToken(accessToken);
        return jwt.Issuer;
    }

    private static string? GetOrgFromClaim(ClaimsPrincipal user)
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

    private static bool ShouldApplyMiddleware(string path)
    {
        return !(path.Contains("/token") || path.Contains("/health"));
    }
}

/// <summary>
/// Static class for middleware registration
/// </summary>
public static class OrgExtractorMiddlewareExtensions
{
    /// <summary>
    /// Registers the <see cref="OrgExtractorMiddleware"/> in the application
    /// </summary>
    public static IApplicationBuilder UseOrgExtractor(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<OrgExtractorMiddleware>();
    }
}
