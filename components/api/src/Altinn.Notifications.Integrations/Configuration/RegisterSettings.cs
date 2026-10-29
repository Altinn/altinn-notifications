namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration settings for the integration with the Altinn Register API.
/// </summary>
public class RegisterSettings
{
    /// <summary>
    /// Gets or sets the base address of the Register API.
    /// </summary>
    public string ApiEndpoint { get; set; } = string.Empty;
}
