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
    /// The maximum number of entries to return in one status feed page.
    /// </summary>
    public int StatusFeedMaxPageSize { get; set; } = 500;

    /// <summary>
    /// The maximum number of SMS notifications claimed and published in one batch.
    /// </summary>
    public int SmsPublishBatchSize { get; set; } = 500;

    /// <summary>
    /// The maximum number of email notifications claimed and published in one batch.
    /// </summary>
    public int EmailPublishBatchSize { get; set; } = 500;

    /// <summary>
    /// The number of expired notifications to terminate per batch.
    /// </summary>
    public int TerminationBatchSize { get; set; } = 100;

    /// <summary>
    /// The maximum number of user-organization pairs per authorization batch call to PDP.
    /// </summary>
    public int AuthorizationBatchSize { get; set; } = 500;

    /// <summary>
    /// Grace period in seconds added to expiry time of notifications, before setting a notification to failed time to live.
    /// </summary>
    public int ExpiryOffsetSeconds { get; set; } = 300;

    /// <summary>
    /// Determines whether to send Sms notifications to the Sms Service via Wolverine and Azure Service Bus or via Kafka
    /// </summary>
    public bool SendSmsNotificationsViaWolverine { get; set; } = false;

    /// <summary>
    /// Determines whether to send Email notifications to Email service via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableSendEmailPublisher { get; set; } = false;
}
