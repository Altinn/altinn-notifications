namespace Altinn.Notifications.Triggers.Configuration;

/// <summary>
/// Configuration class for platform settings
/// </summary>
public class PlatformSettings
{
    /// <summary>
    /// Default from address for email notifications
    /// </summary>
    public string ApiNotificationsEndpoint { get; set; } = string.Empty;
}