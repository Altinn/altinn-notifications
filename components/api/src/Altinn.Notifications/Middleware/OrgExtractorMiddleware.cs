using System.Security.Claims;

using Altinn.Common.AccessToken.Configuration;

using AltinnCore.Authentication.Constants;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Middleware;

/// <summary>
/// Middleware for extracting org information in an HTTP request
/// from either the issuer of a validated platform access token or as
/// an org claim in the bearer token.
/// </summary>
/// <remarks>
/// Must run after authorization. The issuer of a platform access token is only used when
/// the <see cref="Altinn.Common.AccessToken.AccessTokenHandler"/> has validated the token
/// while authorizing the request. The raw header is never read.
/// A request where both the validated access token and the bearer token identify an org,
/// and the two differ, is rejected with 403 Forbidden.
/// </remarks>
public class OrgExtractorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OrgExtractorMiddleware> _logger;
    private readonly string _accessTokenHttpContextId;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgExtractorMiddleware"/> class.
    /// </summary>
    public OrgExtractorMiddleware(RequestDelegate next, ILogger<OrgExtractorMiddleware> logger, IOptions<AccessTokenSettings> accessTokenSettings)
    {
        _next = next;
        _logger = logger;
        _accessTokenHttpContextId = accessTokenSettings.Value.AccessTokenHttpContextId;
    }

    /// <summary>
    /// Retrieve org claim and save in httpContext as Creator item.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldApplyMiddleware(context.Request.Path))
        {
            string? accessTokenOrg = GetIssuerOfValidatedAccessToken(context);
            string? bearerTokenOrg = GetOrgFromClaim(context.User);

            if (accessTokenOrg != null && bearerTokenOrg != null && accessTokenOrg != bearerTokenOrg)
            {
                _logger.LogWarning(
                    "Rejected request to {Path}: platform access token issuer {AccessTokenOrg} does not match bearer token org {BearerTokenOrg}.",
                    context.Request.Path,
                    accessTokenOrg,
                    bearerTokenOrg);

                await Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Conflicting service owner",
                    detail: "The platform access token and the bearer token identify different service owners.")
                    .ExecuteAsync(context);
                return;
            }

            string? org = accessTokenOrg ?? bearerTokenOrg;

            if (org != null)
            {
                context.Items["Org"] = org;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Returns the issuer of the platform access token if, and only if, the token was validated by the
    /// AccessTokenHandler, which stores "{issuer}/{app}" in the HttpContext items after successful validation.
    /// </summary>
    private string? GetIssuerOfValidatedAccessToken(HttpContext context)
    {
        if (context.Items[_accessTokenHttpContextId] is not string accessTokenCredential)
        {
            return null;
        }

        string issuer = accessTokenCredential.Split('/', 2)[0];
        return string.IsNullOrEmpty(issuer) ? null : issuer;
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
        return !(path.Contains("/trigger") || path.Contains("/health"));
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
