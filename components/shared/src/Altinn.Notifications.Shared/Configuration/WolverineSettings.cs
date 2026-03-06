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
    public int ListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for receiving email delivery reports.
    /// Consumed by the API service; produced by the email service and Event Grid.
    /// </summary>
    public string? EmailDeliveryReportQueueName { get; set; }

    /// <summary>
    /// Retry policy for the email delivery report queue.
    /// </summary>
    public QueueRetryPolicy EmailDeliveryReportQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name for receiving SMS delivery reports.
    /// Consumed by the API service; produced by the SMS service delivery report controller.
    /// </summary>
    public string? SmsDeliveryReportQueueName { get; set; }

    /// <summary>
    /// Retry policy for the SMS delivery report queue.
    /// </summary>
    public QueueRetryPolicy SmsDeliveryReportQueuePolicy { get; set; } = new();
}
