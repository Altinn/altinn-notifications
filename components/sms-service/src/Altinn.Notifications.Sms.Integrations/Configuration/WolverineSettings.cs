using Altinn.Notifications.Shared.Configuration;

using LinkMobility.PSWin.Client;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the SMS service.
/// Extends the shared base with queue names and feature flags for the SMS service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// ASB queue name used for publishing SMS messages.
    /// Produced by the API and consumed by the SMS service.
    /// </summary>
    public string SendSmsQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Retry policy for the SMS send queue.
    /// Applies to infrastructure transient errors: <see cref="TimeoutException"/>, <see cref="Azure.Messaging.ServiceBus.ServiceBusException"/>, and <see cref="TaskCanceledException"/>.
    /// </summary>
    public QueueRetryPolicy SendSmsQueuePolicy { get; set; } = new();

    /// <summary>
    /// Retry policy for SMS gateway errors on the SMS send queue.
    /// Applies to <see cref="HttpRequestException"/> (gateway unreachable) and
    /// <see cref="SendMessageException"/> (e.g. 504 rate-limiting responses).
    /// Uses spread-out scheduled retries with no immediate in-lock cooldown retries, to avoid
    /// amplifying load on the SMS gateway during outages or rate-limiting windows.
    /// </summary>
    public QueueRetryPolicy SendSmsQueueGatewayErrorPolicy { get; set; } = new();

    /// <summary>
    /// Number of concurrent listeners for the SMS send queue per pod.
    /// </summary>
    public int SendSmsListenerCount { get; set; } = 10;

    /// <summary>
    /// Determines whether SMS send commands are consumed via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableSendSmsListener { get; set; } = true;
    
    /// <summary>
    /// Determines whether to publish SMS delivery reports via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableSmsDeliveryReportPublisher { get; set; } = true;

    /// <summary>
    /// ASB queue name for publishing SMS delivery reports.
    /// Consumed by the API service's <c>SmsDeliveryReportHandler</c>.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;

    /// <summary>
    /// Determines whether to publish SMS send results via Wolverine and Azure Service Bus.
    /// </summary>
    public bool EnableSmsSendResultPublisher { get; set; } = true;

    /// <summary>
    /// ASB queue name for publishing SMS send results.
    /// Produced by this SMS service and consumed by the Notifications API.
    /// </summary>
    public string SmsSendResultQueueName { get; set; } = string.Empty;
}
