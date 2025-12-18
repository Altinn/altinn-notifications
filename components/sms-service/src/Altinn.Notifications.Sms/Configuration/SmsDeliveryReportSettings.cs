namespace Altinn.Notifications.Sms.Configuration;

/// <summary>
/// Configuration object used to hold settings for the delivery report endpoint.
/// </summary>
public class SmsDeliveryReportSettings
{
    /// <summary>
    /// The user settings
    /// </summary> 
    public UserSettings UserSettings { get; set; } = new();

    /// <summary>
    /// Toggles whether delivery reports should be logged to Application Insights
    /// using a custom middleware that intercepts the incoming HTTP requests.
    /// </summary>
    public bool LogDeliveryReportsToApplicationInsights { get; set; } = false;
}

/// <summary>
/// Configuration object used to hold user settings to access endpoint.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// The username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
