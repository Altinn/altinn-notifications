using System.Collections.Immutable;
using System.Text.Json;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="IEmailNotificationService"/>.
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly IGuidService _guidService;
    private readonly int _emailPublishBatchSize;
    private readonly string _emailSendTopicName;
    private readonly string _emailSendQueueName;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IDateTimeService _dateTimeService;
    private readonly IEmailNotificationRepository _emailNotificationRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationService"/> class.
    /// </summary>
    public EmailNotificationService(
        IGuidService guid,
        IKafkaProducer producer,
        IDateTimeService dateTime,
        IOptions<KafkaSettings> kafkaSettings,
        IOptions<NotificationConfig> notificationConfig,
        IEmailNotificationRepository repository)
    {
        _guidService = guid;
        _kafkaProducer = producer;
        _dateTimeService = dateTime;
        _emailNotificationRepository = repository;
        _emailSendTopicName = kafkaSettings.Value.EmailQueueTopicName;
        _emailPublishBatchSize = notificationConfig.Value.EmailPublishBatchSize;
    }

    /// <inheritdoc/>
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, List<EmailAddressPoint> emailAddresses, EmailRecipient emailRecipient, bool ignoreReservation = false)
    {
        if (emailRecipient.IsReserved.HasValue && emailRecipient.IsReserved.Value && !ignoreReservation)
        {
            emailRecipient.ToAddress = string.Empty; // not persisting email address for reserved recipients
            await CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.Failed_RecipientReserved);
            return;
        }

        if (emailAddresses.Count == 0)
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.Failed_RecipientNotIdentified);
            return;
        }

        foreach (EmailAddressPoint addressPoint in emailAddresses)
        {
            emailRecipient.ToAddress = addressPoint.EmailAddress;

            await CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.New);
        }
    }

    /// <inheritdoc/>
    public async Task TerminateExpiredNotifications()
    {
        // process hanging notifications that have been set to succeeded, but never reached a final stage
        await _emailNotificationRepository.TerminateExpiredNotifications();
    }

    /// <inheritdoc/>
    public async Task SendNotifications(CancellationToken cancellationToken)
    {
        List<Email> newEmailNotifications;

        do
        {
            newEmailNotifications = [];

            try
            {
                newEmailNotifications = await _emailNotificationRepository.GetNewNotificationsAsync(_emailPublishBatchSize, cancellationToken);
                if (newEmailNotifications.Count == 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var serializedEmailNotifications = newEmailNotifications.Select(readyToSendEmail => readyToSendEmail.Serialize()).ToImmutableList();

                var unpublishedEmailNotifications = await _kafkaProducer.ProduceAsync(_emailSendTopicName, serializedEmailNotifications, cancellationToken);

                foreach (var unpublishedEmailNotification in unpublishedEmailNotifications)
                {
                    var deserializedEmailNotification = JsonSerializer.Deserialize<Email>(unpublishedEmailNotification, JsonSerializerOptionsProvider.Options);
                    if (deserializedEmailNotification == null || deserializedEmailNotification.NotificationId == Guid.Empty)
                    {
                        continue;
                    }

                    await _emailNotificationRepository.UpdateSendStatus(deserializedEmailNotification.NotificationId, EmailNotificationResultType.New);
                }
            }
            catch (OperationCanceledException)
            {
                foreach (var email in newEmailNotifications)
                {
                    await _emailNotificationRepository.UpdateSendStatus(email.NotificationId, EmailNotificationResultType.New);
                }

                throw;
            }
        }
        while (newEmailNotifications.Count > 0);
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(EmailSendOperationResult sendOperationResult)
    {
        // set to new to allow new iteration of regular processing if transient error
        if (sendOperationResult.SendResult == EmailNotificationResultType.Failed_TransientError)
        {
            sendOperationResult.SendResult = EmailNotificationResultType.New;
        }

        await _emailNotificationRepository.UpdateSendStatus(sendOperationResult.NotificationId, (EmailNotificationResultType)sendOperationResult.SendResult!, sendOperationResult.OperationId);
    }

    /// <summary>
    /// Creates a notification for a recipient.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="requestedSendTime">The requested send time.</param>
    /// <param name="recipient">The email recipient.</param>
    /// <param name="result">The result of the notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, EmailRecipient recipient, EmailNotificationResultType result)
    {
        var emailNotification = new EmailNotification()
        {
            OrderId = orderId,
            Id = _guidService.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(result, _dateTimeService.UtcNow())
        };

        DateTime expiry;

        if (result == EmailNotificationResultType.Failed_RecipientNotIdentified)
        {
            expiry = _dateTimeService.UtcNow();
        }
        else
        {
            expiry = requestedSendTime.AddHours(48);
        }

        await _emailNotificationRepository.AddNotification(emailNotification, expiry);
    }
}
