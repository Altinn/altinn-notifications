#nullable enable

namespace Altinn.Notifications.Core.Shared;

/// <summary>
/// Interface describing the contract for an authentication context, which can be used to manage and access
/// authentication-related information within the application.
/// </summary>
public interface IAuthenticationContext
{
    /// <summary>
    /// Retrieves the JWT token from the current authentication context.
    /// </summary>
    /// <returns>The JWT token string if available; otherwise, null.</returns>
    public string? GetTokenFromContext();
}
