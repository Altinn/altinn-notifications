namespace Altinn.Notifications.Configuration;

/// <summary>
/// Configuration object used to hold general settings for the notifications application.
/// </summary>
public class GeneralSettings
{
    /// <summary>
    /// Base Uri
    /// </summary>
    public string BaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Open Id Connect Well known endpoint
    /// </summary>
    public string OpenIdWellKnownEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Name of the cookie for where JWT is stored
    /// </summary>
    public string JwtCookieName { get; set; } = string.Empty;
}