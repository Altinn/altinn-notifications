#nullable enable

using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Authorization;

/// <summary>
/// An implementation of the <see cref="IAuthenticationContext"/> interface that provides access to the
/// authentication context.
/// </summary>
public class AuthenticationContext : IAuthenticationContext
{
    private const string _authorizationHeaderName = "Authorization";
    private const string _bearerPrefix = "Bearer ";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationContext"/> class.
    /// </summary>
    public AuthenticationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Retrieves the JWT token from authorization header of the current request via the HttpContext.
    /// </summary>
    /// <returns>The JWT token if available; otherwise, <c>null</c>.</returns>
    public string? GetTokenFromContext()
    {
        string? authorization = _httpContextAccessor.HttpContext?.Request.Headers[_authorizationHeaderName];

        if (string.IsNullOrEmpty(authorization))
        {
            return null;
        }

        if (authorization.StartsWith(_bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorization[_bearerPrefix.Length..].Trim();
        }

        return null;
    }
}
