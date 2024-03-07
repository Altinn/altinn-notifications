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
}
