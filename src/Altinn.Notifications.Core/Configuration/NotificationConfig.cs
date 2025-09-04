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
    /// Maximum number of SMS notifications to process in a single batch when transitioning from "new" to "sending" status and publishing to Kafka.
    /// </summary>
    /// <remarks>
    /// Setting an appropriate batch size helps optimize performance and resource utilization during high-volume processing.
    /// The default value is 50 notifications per batch.
    /// </remarks>
    public int SmsPublishBatchSize { get; set; } = 50;
}
