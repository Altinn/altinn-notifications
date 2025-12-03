namespace Altinn.Notifications.Email.Configuration;

/// <summary>
/// Configuration object used to hold authorization settings for the delivery report controller.
/// </summary>
public class EmailDeliveryReportSettings
{
    /// <summary>
    /// Acceskey to be used for authorization
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Toggles whether delivery reports should be logged to Application Insights
    /// using a custom middleware that intercepts the incoming HTTP requests.
    /// </summary>
    public bool LogDeliveryReportsToApplicationInsights { get; set; } = false;

    /// <summary>
    /// The type of object to parse from the incoming request body.
    /// </summary>
    public string ParseObject { get; set; } = "deliveryreport";
}
