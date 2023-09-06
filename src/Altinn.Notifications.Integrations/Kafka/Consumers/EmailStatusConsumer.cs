using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for status messages about email notifications
/// </summary>
public class EmailStatusConsumer : KafkaConsumerBase<EmailStatusConsumer>
{
    private readonly IEmailNotificationService _emailNotificationsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusConsumer"/> class.
    /// </summary>
    public EmailStatusConsumer(IEmailNotificationService emailNotificationsService, IOptions<KafkaSettings> settings, ILogger<EmailStatusConsumer> logger)
        : base(settings, logger, settings.Value.EmailStatusUpdatedTopicName)
    {
        _emailNotificationsService = emailNotificationsService;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessOrder, RetryOrder, stoppingToken), stoppingToken);
    }

    private async Task ProcessOrder(string message)
    {
        bool succeeded = SendOperationResult.TryParse(message, out SendOperationResult result);

        if (!succeeded)
        {
            return;
        }

        await _emailNotificationsService.UpdateStatusNotification(result);
    }

    private async Task RetryOrder(string message)
    {
        // Retry logic - back to kafka?
        await Task.CompletedTask;
    }
}