using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing email status retry messages
/// </summary>
public sealed class EmailStatusRetryConsumer(IKafkaProducer producer, IDeadDeliveryReportService deadDeliveryReportService, IOptions<Configuration.KafkaSettings> settings, ILogger<EmailStatusRetryConsumer> logger)
    : NotificationStatusRetryConsumerBase<EmailStatusRetryConsumer>(producer, deadDeliveryReportService, settings, logger, settings.Value.EmailStatusUpdatedRetryTopicName)
{
    /// <inheritdoc/>
    protected override DeliveryReportChannel Channel => DeliveryReportChannel.AzureCommunicationServices;
}
