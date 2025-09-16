using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for processing status messages about email notifications.
/// </summary>
public sealed class EmailStatusConsumer : NotificationStatusConsumerBase<EmailStatusConsumer, EmailSendOperationResult>
{
    private readonly IEmailNotificationService _emailNotificationsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusConsumer"/> class.
    /// </summary>
    public EmailStatusConsumer(
        IKafkaProducer producer,
        IMemoryCache memoryCache,
        IOptions<KafkaSettings> settings,
        ILogger<EmailStatusConsumer> logger,
        IEmailNotificationService emailNotificationsService)
        : base(settings.Value.EmailStatusUpdatedTopicName, settings.Value.EmailStatusUpdatedTopicName, producer, memoryCache, settings, logger)
    {
        _emailNotificationsService = emailNotificationsService;
    }

    /// <summary>
    /// Gets the name of the notification channel being processed.
    /// </summary>
    /// <returns>The string "email" representing the email notification channel.</returns>
    protected override string ChannelName => "email";

    /// <summary>
    /// Attempts to parse a message into an <see cref="EmailSendOperationResult"/> object.
    /// </summary>
    /// <param name="message">The message to parse.</param>
    /// <param name="result">The parsed result if successful; otherwise, null.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    protected override bool TryParse(string message, out EmailSendOperationResult result) => EmailSendOperationResult.TryParse(message, out result);

    /// <summary>
    /// Updates the email notification status based on the parsed result.
    /// </summary>
    /// <param name="result">The parsed result containing email status update information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task UpdateStatusAsync(EmailSendOperationResult result) => _emailNotificationsService.UpdateSendStatus(result);

    /// <summary>
    /// Gets a key used for suppressing duplicate log entries for the same error.
    /// </summary>
    /// <param name="result">The parsed result that failed to update.</param>
    /// <param name="exception">The exception that occurred during status update (unused in this implementation).</param>
    /// <returns>A string key used for log suppression, using either the operation ID or notification ID.</returns>
    protected override string? GetSuppressionKey(EmailSendOperationResult result, SendStatusUpdateException exception) => $"{exception.IdentifierType}:{exception.Identifier}";
}
