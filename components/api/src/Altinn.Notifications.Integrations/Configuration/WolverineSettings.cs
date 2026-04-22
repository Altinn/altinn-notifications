using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the Notifications API.
/// Extends the shared base with queue names produced and consumed by the API.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// Maximum number of email send commands published concurrently during a batch publish operation.
    /// </summary>
    public int EmailPublishConcurrency { get; set; } = 10;

    /// <summary>
    /// Determines whether to publish email send commands via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableSendEmailPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for publishing email send commands.
    /// Produced by this API and consumed by the email service.
    /// </summary>
    public string EmailSendQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email send queue.
    /// </summary>
    public QueueRetryPolicy EmailSendQueuePolicy { get; set; } = new();

    /// <summary>
    /// ASB queue name used for publishing sms messages.
    /// Produced by the API and consumed by the SMS Service and service provider.
    /// </summary>
    public string SendSmsQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enable the email delivery report queue listener.
    /// </summary>
    public bool EnableEmailDeliveryReportListener { get; set; } = false;

    /// <summary>
    /// ASB queue name for receiving email delivery reports.
    /// Produced by the email service and Event Grid and consumed by this API.
    /// </summary>
    public string EmailDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email delivery report queue.
    /// </summary>
    public QueueRetryPolicy EmailDeliveryReportQueuePolicy { get; set; } = new();

    /// <summary>
    /// Maximum number of SMS send commands published concurrently during a batch publish operation.
    /// </summary>
    public int SmsPublishConcurrency { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the SMS publisher is enabled.
    /// </summary>
    public bool EnableSendSmsPublisher { get; set; } = false;

    /// <summary>
    /// Whether to enable the SMS delivery report queue listener.
    /// </summary>
    public bool EnableSmsDeliveryReportListener { get; set; } = false;

    /// <summary>
    /// ASB queue name for receiving SMS delivery reports.
    /// Published by the SMS service when <c>EnableSmsDeliveryReportPublisher</c> is <c>true</c> there.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS delivery report queue.
    /// </summary>
    public QueueRetryPolicy SmsDeliveryReportQueuePolicy { get; set; } = new();

    /// <summary>
    /// Determines whether to consume email send results via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnableEmailSendResultListener { get; set; } = false;

    /// <summary>
    /// ASB queue name for receiving email send results.
    /// Produced by the email service and consumed by this API.
    /// </summary>
    public string EmailSendResultQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email send result queue.
    /// </summary>
    public QueueRetryPolicy EmailSendResultQueuePolicy { get; set; } = new();

    /// <summary>
    /// Determines whether to publish past-due order commands via Wolverine and Azure Service Bus or via Kafka.
    /// </summary>
    public bool EnablePastDueOrderPublisher { get; set; } = false;

    /// <summary>
    /// Determines whether to listen for past-due order commands from the Azure Service Bus queue.
    /// </summary>
    public bool EnablePastDueOrderListener { get; set; } = false;

    /// <summary>
    /// ASB queue name for past-due order processing commands.
    /// Both produced and consumed by this API (internal queue).
    /// </summary>
    public string PastDueOrdersQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the past-due orders queue.
    /// </summary>
    public QueueRetryPolicy PastDueOrdersQueuePolicy { get; set; } = new();
}
