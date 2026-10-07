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
    /// The maximum number of composed email notifications claimed and published in one batch.
    /// Defaults to 50 (vs 500 for standard emails) because each composed email triggers
    /// concurrent blob downloads at send time, making the per-message cost significantly higher.
    /// </summary>
    public int ComposedEmailPublishBatchSize { get; set; } = 50;

    /// <summary>
    /// The number of expired notifications to terminate per batch.
    /// </summary>
    public int TerminationBatchSize { get; set; } = 100;

    /// <summary>
    /// The number of status feed records to delete per cleanup job invocation.
    /// </summary>
    public int StatusFeedCleanupBatchSize { get; set; } = 10000;

    /// <summary>
    /// The maximum number of user-organization pairs per authorization batch call to PDP.
    /// </summary>
    public int AuthorizationBatchSize { get; set; } = 500;

    /// <summary>
    /// Grace period in seconds added to expiry time of notifications, before setting a notification to failed time to live.
    /// </summary>
    public int ExpiryOffsetSeconds { get; set; } = 300;

    /// <summary>
    /// The number of seconds from now to skip while looking for orders to retry
    /// </summary>
    public int RetryOrdersDBDelaySeconds { get; set; } = 60;

    /// <summary>
    /// The maximum number of retry attempts for a retry order.
    /// </summary>
    public int RetryOrdersMaxCount { get; set; } = 10;

    /// <summary>
    /// Configuration for the process loop of the past due orders background service
    /// </summary>
    public ProcessLoopConfig PastDueOrdersProcessLoopConfig { get; set; } = new()
    {
        TaskCount = 30,
        PrimaryTaskIdleDelaySeconds = 30,
        AdditionalTasksIdleDelaySeconds = 5,
        RampUpLimit = 10
    };

    /// <summary>
    /// Configuration for the process loop of the retry orders background service
    /// </summary>
    public ProcessLoopConfig RetryOrdersProcessLoopConfig { get; set; } = new()
    {
        TaskCount = 1,
        PrimaryTaskIdleDelaySeconds = 30,
        AdditionalTasksIdleDelaySeconds = 5,
        RampUpLimit = 10
    };

    /// <summary>
    /// Configuration for the process loop of the email notifications background service
    /// </summary>
    public ProcessLoopConfig EmailNotificationsProcessLoopConfig { get; set; } = new()
    {
        TaskCount = 30,
        PrimaryTaskIdleDelaySeconds = 30,
        AdditionalTasksIdleDelaySeconds = 5,
        RampUpLimit = 10
    };

    /// <summary>
    /// Configuration for the process loop of the composed email notifications background service
    /// </summary>
    public ProcessLoopConfig ComposedEmailNotificationsProcessLoopConfig { get; set; } = new()
    {
        TaskCount = 30,
        PrimaryTaskIdleDelaySeconds = 30,
        AdditionalTasksIdleDelaySeconds = 5,
        RampUpLimit = 10
    };

    /// <summary>
    /// Configuration for the process loop of the sms daytime notifications background service
    /// </summary>
    public ProcessLoopConfig SmsDaytimeNotificationsProcessLoopConfig { get; set; } = new()
    {
        TaskCount = 30,
        PrimaryTaskIdleDelaySeconds = 30,
        AdditionalTasksIdleDelaySeconds = 5,
        RampUpLimit = 10
    };

    /// <summary>
    /// Configuration for the process loop of the sms anytime notifications background service
    /// </summary>
    public ProcessLoopConfig SmsAnytimeNotificationsProcessLoopConfig { get; set; } = new()
    {
        TaskCount = 30,
        PrimaryTaskIdleDelaySeconds = 30,
        AdditionalTasksIdleDelaySeconds = 5,
        RampUpLimit = 10
    };
}

/// <summary>
/// Configuration class for process loop settings
/// </summary>
public class ProcessLoopConfig
{
    /// <summary>
    /// The number of tasks to run concurrently in the background service
    /// </summary>
    public int TaskCount { get; set; } = 30;

    /// <summary>
    /// The delay in seconds between each iteration of the background service task when idle for the primary task.
    /// The primary task is the first task that is started and is responsible for triggering additional tasks if needed.
    /// </summary>
    public int PrimaryTaskIdleDelaySeconds { get; set; } = 30;

    /// <summary>
    /// The delay in seconds between each iteration of the background service task for all other tasks
    /// than the primary task.
    /// </summary>
    public int AdditionalTasksIdleDelaySeconds { get; set; } = 5;

    /// <summary>
    /// The number of consecutive tasks before ramping up the processing of tasks. This is used to prevent overloading the system with too many tasks at once.
    /// </summary>
    public int RampUpLimit { get; set; } = 10;
}
