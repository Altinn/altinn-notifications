namespace Altinn.Notifications.Shared.Configuration;

/// <summary>
/// Represents settings for configuring Wolverine with Azure Service Bus.
/// All three notification services (API, email, SMS) reference this shared class.
/// Each service only configures the queue names it uses; unused properties remain null.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// Indicates whether Azure Service Bus should be configured.
    /// Defaults to false — Kafka remains active when this is false.
    /// </summary>
    public bool EnableServiceBus { get; set; } = false;

    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Number of listeners per queue per pod.
    /// </summary>
    public int ListenerCount { get; set; } = 1;

    /// <summary>
    /// ASB queue name for email delivery status updates.
    /// Consumed by the API service; produced by the email service and Event Grid.
    /// </summary>
    public string? EmailStatusQueueName { get; set; }

    /// <summary>
    /// Retry policy for the email status queue.
    /// </summary>
    public QueueRetryPolicy EmailStatusQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for SMS delivery status updates.
    /// Consumed by the API service; produced by the SMS service delivery report controller.
    /// </summary>
    public string? SmsStatusQueueName { get; set; }

    /// <summary>
    /// Retry policy for the SMS status queue.
    /// </summary>
    public QueueRetryPolicy SmsStatusQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for accepted email send operations (polling loop).
    /// Consumed by the email service.
    /// </summary>
    public string? EmailSendingAcceptedQueueName { get; set; }

    /// <summary>
    /// Retry policy for the email sending accepted queue.
    /// </summary>
    public QueueRetryPolicy EmailSendingAcceptedQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for past-due order processing.
    /// Published and consumed by the API service.
    /// </summary>
    public string? PastDueOrdersQueueName { get; set; }

    /// <summary>
    /// Retry policy for the past-due orders queue.
    /// </summary>
    public QueueRetryPolicy PastDueOrdersQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for the email send queue.
    /// Published by the API service; consumed by the email service.
    /// </summary>
    public string? EmailSendQueueName { get; set; }

    /// <summary>
    /// Retry policy for the email send queue.
    /// </summary>
    public QueueRetryPolicy EmailSendQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for the SMS send queue.
    /// Published by the API service; consumed by the SMS service.
    /// </summary>
    public string? SmsSendQueueName { get; set; }

    /// <summary>
    /// Retry policy for the SMS send queue.
    /// </summary>
    public QueueRetryPolicy SmsSendQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for Altinn service availability updates (e.g. ACS rate limiting).
    /// Published by the email service; consumed by the API service.
    /// </summary>
    public string? AltinnServiceUpdateQueueName { get; set; }

    /// <summary>
    /// Retry policy for the Altinn service update queue.
    /// </summary>
    public QueueRetryPolicy AltinnServiceUpdateQueuePolicy { get; set; } = new();
}
