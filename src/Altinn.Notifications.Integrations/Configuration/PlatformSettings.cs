namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration object used to hold settings for all Altinn Platform integrations.
/// </summary>
public class PlatformSettings
{
    /// <summary>
    /// Gets or sets the url for the profile API
    /// </summary>
    public string ApiProfileEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the url for the register API
    /// </summary>
    public string ApiRegisterEndpoint { get; set; } = string.Empty;
}
