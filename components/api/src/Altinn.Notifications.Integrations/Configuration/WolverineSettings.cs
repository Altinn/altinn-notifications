using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings for the Notifications API.
/// Extends the shared base with queue names consumed by the API.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// Maximum number of email send commands published concurrently during a batch publish operation.
    /// </summary>
    public int EmailPublishConcurrency { get; set; } = 10;

    /// <summary>
    /// Whether to enable the email send publisher.
    /// </summary>
    public bool EnableSendEmailPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name used for publishing email messages.
    /// Produced by the API and consumed by the email service and Azure Communication Services.
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
    /// Produced by the email service and Event Grid.
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
}
