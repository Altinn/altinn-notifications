namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration object used to hold settings for all Altinn Platform integrations.
/// </summary>
public class PlatformSettings
{
    /// <summary>
    /// Gets or sets the url for the API profile endpoint
    /// </summary>
    public string ApiProfileEndpoint { get; set; } = string.Empty;
}
