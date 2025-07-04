﻿using Altinn.Notifications.Core.Configuration;
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
    private readonly IDateTimeService _dateTime;
    private readonly IGuidService _guid;
    private readonly string _emailQueueTopicName;
    private readonly IEmailNotificationRepository _repository;
    private readonly IKafkaProducer _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationService"/> class.
    /// </summary>
    public EmailNotificationService(
        IGuidService guid,
        IKafkaProducer producer,
        IDateTimeService dateTime,
        IOptions<KafkaSettings> kafkaSettings,
        IEmailNotificationRepository repository)
    {
        _guid = guid;
        _dateTime = dateTime;
        _producer = producer;
        _repository = repository;
        _emailQueueTopicName = kafkaSettings.Value.EmailQueueTopicName;
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
        await _repository.TerminateExpiredNotifications();
    }

    /// <inheritdoc/>
    public async Task SendNotifications()
    {
        List<Email> emails = await _repository.GetNewNotifications();

        foreach (Email email in emails)
        {
            bool success = await _producer.ProduceAsync(_emailQueueTopicName, email.Serialize());
            if (!success)
            {
                await _repository.UpdateSendStatus(email.NotificationId, EmailNotificationResultType.New);
            }
        }
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(EmailSendOperationResult sendOperationResult)
    {
        // set to new to allow new iteration of regular processing if transient error
        if (sendOperationResult.SendResult == EmailNotificationResultType.Failed_TransientError)
        {
            sendOperationResult.SendResult = EmailNotificationResultType.New;
        }

        await _repository.UpdateSendStatus(sendOperationResult.NotificationId, (EmailNotificationResultType)sendOperationResult.SendResult!, sendOperationResult.OperationId);
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
            Id = _guid.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(result, _dateTime.UtcNow())
        };

        DateTime expiry;

        if (result == EmailNotificationResultType.Failed_RecipientNotIdentified)
        {
            expiry = _dateTime.UtcNow();
        }
        else
        {
            expiry = requestedSendTime.AddHours(48);
        }

        await _repository.AddNotification(emailNotification, expiry);
    }
}
