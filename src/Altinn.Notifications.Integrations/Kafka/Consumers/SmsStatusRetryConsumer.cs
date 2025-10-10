using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing SMS status retry messages
/// </summary>
public sealed class SmsStatusRetryConsumer(
    IKafkaProducer producer,
    IDeadDeliveryReportService deadDeliveryReportService,
    IOptions<Configuration.KafkaSettings> settings,
    ILogger<NotificationStatusRetryConsumerBase> logger)
    : NotificationStatusRetryConsumerBase(
        producer,
        deadDeliveryReportService,
        settings,
        settings.Value.SmsStatusUpdatedRetryTopicName,
        logger)
{
    /// <summary>
    /// Gets the delivery report channel for Link Mobility SMS notifications.
    /// </summary>
    protected override DeliveryReportChannel Channel => DeliveryReportChannel.LinkMobility;

    /// <summary>
    /// Executes the SMS status retry consumer to process messages from the Kafka topic
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the consumer</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }
}
