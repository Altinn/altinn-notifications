#nullable enable

namespace Altinn.Notifications.Integrations.Dialogporten;

/// <summary>
/// Settings required for configuring the Dialogporten integration.
/// </summary>
public class DialogportenSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the Dialogporten integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the base URL of the Dialogporten service.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
