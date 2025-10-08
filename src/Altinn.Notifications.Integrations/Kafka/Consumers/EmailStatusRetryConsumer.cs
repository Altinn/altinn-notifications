using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing email status retry messages
/// </summary>
public sealed class EmailStatusRetryConsumer(IKafkaProducer producer, IDeadDeliveryReportService deadDeliveryReportService, IOptions<Configuration.KafkaSettings> settings, ILogger<EmailStatusRetryConsumer> logger)
    : StatusRetryConsumerBase(producer, deadDeliveryReportService, settings, settings.Value.EmailStatusUpdatedRetryTopicName, logger)
{
    /// <summary>
    /// Gets the delivery report channel for Azure Communication Services email notifications.
    /// </summary>
    protected override DeliveryReportChannel Channel => DeliveryReportChannel.AzureCommunicationServices;

    /// <summary>
    /// Executes the email status retry consumer to process messages from the Kafka topic
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the consumer</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }
}
