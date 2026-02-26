namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration settings for Altinn Platform service integrations.
/// Contains endpoint URLs for various platform APIs used within the notifications API.
/// </summary>
public class PlatformSettings
{
    /// <summary>
    /// Gets or sets the URL for the Profile API.
    /// </summary>
    public string ApiProfileEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL for the Register API.
    /// </summary>
    public string ApiRegisterEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL for the Altinn Notifications SMS API.
    /// </summary>
    public string ApiShortMessageServiceEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL for the Altinn Notifications Email API.
    /// </summary>
    public string ApiInstantEmailServiceEndpoint { get; set; } = string.Empty;
}
