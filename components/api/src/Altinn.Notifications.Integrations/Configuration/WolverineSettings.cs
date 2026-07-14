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
    /// ASB queue name for publishing email send commands.
    /// Produced by this API and consumed by the email service.
    /// </summary>
    public string EmailSendQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of composed email send commands published concurrently during a batch publish operation.
    /// </summary>
    public int ComposedEmailPublishConcurrency { get; set; } = 5;

    /// <summary>
    /// ASB queue name for publishing composed email send commands.
    /// Produced by this API and consumed by the email service on a dedicated listener.
    /// </summary>
    public string ComposedEmailSendQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of SMS send commands published concurrently during a batch publish operation.
    /// </summary>
    public int SmsPublishConcurrency { get; set; } = 10;

    /// <summary>
    /// ASB queue name used for publishing SMS messages.
    /// Produced by the API and consumed by the SMS Service and service provider.
    /// </summary>
    public string SendSmsQueueName { get; set; } = string.Empty;

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
    /// Number of concurrent listeners for the email delivery report queue per pod.
    /// </summary>
    public int EmailDeliveryReportListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for receiving SMS delivery reports.
    /// Produced by the SMS service and consumed by this API.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS delivery report queue.
    /// </summary>
    public QueueRetryPolicy SmsDeliveryReportQueuePolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the SMS delivery report queue per pod.
    /// </summary>
    public int SmsDeliveryReportListenerCount { get; set; } = 10;

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
    /// Number of concurrent listeners for the email send result queue per pod.
    /// </summary>
    public int EmailSendResultListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for receiving SMS send results.
    /// Produced by the SMS service and consumed by this API.
    /// </summary>
    public string SmsSendResultQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS send result queue.
    /// </summary>
    public QueueRetryPolicy SmsSendResultQueuePolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the SMS send result queue per pod.
    /// </summary>
    public int SmsSendResultListenerCount { get; set; } = 10;

    /// <summary>
    /// ASB queue name for receiving email service rate limit notifications.
    /// Published by the email service and consumed by the API to update service availability.
    /// </summary>
    public string EmailServiceRateLimitQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the email service rate limit queue.
    /// </summary>
    public QueueRetryPolicy EmailServiceRateLimitQueuePolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the email service rate limit queue per pod.
    /// </summary>
    public int EmailServiceRateLimitListenerCount { get; set; } = 1;

    /// <summary>
    /// ASB queue name for past-due order processing commands.
    /// Both produced and consumed by this API (internal queue).
    /// </summary>
    public string PastDueOrdersQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the past-due orders queue.
    /// </summary>
    public QueueRetryPolicy PastDueOrdersQueuePolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the past-due orders queue per pod.
    /// </summary>
    public int PastDueOrdersListenerCount { get; set; } = 10;

    /// <summary>
    /// Delay in milliseconds before the single scheduled retry for inconclusive send conditions
    /// and platform dependency failures.
    /// </summary>
    public int PastDueOrdersRetryDelayMs { get; set; } = 60_000;

    /// <summary>
    /// Maximum number of past-due orders send commands published concurrently during a batch publish operation.
    /// </summary>
    public int PastDueOrdersPublishConcurrency { get; set; } = 10;
}
