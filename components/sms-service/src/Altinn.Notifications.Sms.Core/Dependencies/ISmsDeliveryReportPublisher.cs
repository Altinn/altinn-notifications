using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Core.Dependencies;

/// <summary>
/// Describes the required public method of the SMS delivery report publisher.
/// Implementations publish <see cref="SendOperationResult"/> payloads
/// either to Kafka (default) or to Azure Service Bus when the ASB flag is enabled.
/// </summary>
public interface ISmsDeliveryReportPublisher
{
    /// <summary>
    /// Publishes an SMS delivery report.
    /// </summary>
    /// <param name="result">The result of the SMS delivery operation.</param>
    Task PublishAsync(SendOperationResult result);
}
