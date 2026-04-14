using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the SMS service.
/// Extends the shared base with queue names and feature flags for the SMS service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
    /// <summary>
    /// When <c>true</c>, <c>StatusService</c> publishes SMS delivery reports to
    /// the ASB queue instead of the Kafka topic.
    /// Must match <c>WolverineSettings</c> in the SMS core project.
    /// </summary>
    public bool EnableSmsDeliveryReportPublisher { get; set; } = false;

    /// <summary>
    /// ASB queue name for publishing SMS delivery reports.
    /// Consumed by the API service's <c>SmsDeliveryReportHandler</c>.
    /// </summary>
    public string SmsDeliveryReportQueueName { get; set; } = string.Empty;
}
