using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing email status retry messages
/// </summary>
public sealed class EmailStatusRetryConsumer(
    IKafkaProducer producer,
    IEmailNotificationService emailNotificationService,
    IDeadDeliveryReportService deadDeliveryReportService,
    IOptions<Configuration.KafkaSettings> settings,
    ILogger<EmailStatusRetryConsumer> logger)
    : NotificationStatusRetryConsumerBase(settings.Value.EmailStatusUpdatedRetryTopicName, producer, settings, deadDeliveryReportService, logger)
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

    /// <summary>
    /// Updates the email notification status based on the retry message payload.
    /// </summary>
    /// <param name="retryMessage">The message object containing both metadata and send oepration result payload</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Throws an InvalidOperationException when the payload could not be parsed</exception>
    protected override async Task UpdateStatusAsync(UpdateStatusRetryMessage retryMessage)
    {
        var emailSendOperationResult = JsonSerializer.Deserialize<EmailSendOperationResult>(retryMessage.SendOperationResult, JsonSerializerOptionsProvider.Options) ?? throw new InvalidOperationException("Deserialization of EmailSendOperationResult failed.");

        await emailNotificationService.UpdateSendStatus(emailSendOperationResult);
    }
}
