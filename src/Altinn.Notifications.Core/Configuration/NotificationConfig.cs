namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration class for notification orders
/// </summary>
public class NotificationConfig
{
    /// <summary>
    /// Default from address for email notifications
    /// </summary>
    public string DefaultEmailFromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Default sender number for sms notifications
    /// </summary>
    public string DefaultSmsSenderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Start hour of the SMS send window
    /// </summary>
    public int SmsSendWindowStartHour { get; set; } = 9;

    /// <summary>
    /// End hour of the SMS send window
    /// </summary>
    public int SmsSendWindowEndHour { get; set; } = 17;

    /// <summary>
    /// Maximum number of SMS notifications claimed and published in one batch.
    /// </summary>
    public int SmsPublishBatchSize { get; set; } = 100;
}
