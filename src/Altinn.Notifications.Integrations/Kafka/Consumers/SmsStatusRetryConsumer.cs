using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing SMS status retry messages
/// </summary>
public sealed class SmsStatusRetryConsumer(IKafkaProducer producer, IDeadDeliveryReportService deadDeliveryReportService, IOptions<Configuration.KafkaSettings> settings, ILogger<SmsStatusRetryConsumer> logger)
    : NotificationStatusRetryConsumerBase<SmsStatusRetryConsumer>(producer, deadDeliveryReportService, settings, logger, settings.Value.SmsStatusUpdatedRetryTopicName)
{
    /// <inheritdoc/>
    protected override DeliveryReportChannel Channel => DeliveryReportChannel.LinkMobility;
}
