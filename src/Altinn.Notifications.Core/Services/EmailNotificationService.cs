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
/// Implementation of <see cref="IEmailNotificationService"/>
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly IGuidService _guid;
    private readonly IDateTimeService _dateTime;
    private readonly IEmailNotificationRepository _repository;
    private readonly IKafkaProducer _producer;
    private readonly string _emailQueueTopicName;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationService"/> class.
    /// </summary>
    public EmailNotificationService(
        IGuidService guid,
        IDateTimeService dateTime,
        IEmailNotificationRepository repository,
        IKafkaProducer producer,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _guid = guid;
        _dateTime = dateTime;
        _repository = repository;
        _producer = producer;
        _emailQueueTopicName = kafkaSettings.Value.EmailQueueTopicName;
    }

    /// <inheritdoc/>
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient, bool ignoreReservation = false)
    {
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        EmailRecipient emailRecipient = new()
        {
            OrganizationNumber = recipient.OrganizationNumber,
            NationalIdentityNumber = recipient.NationalIdentityNumber,
            ToAddress = addressPoint?.EmailAddress ?? string.Empty,
            IsReserved = recipient.IsReserved
        };

        if (recipient.IsReserved.HasValue && recipient.IsReserved.Value && !ignoreReservation)
        {
            emailRecipient.ToAddress = string.Empty; // not persisting email address for reserved recipients
            await CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.Failed_RecipientReserved);
            return;
        }
        else if (string.IsNullOrEmpty(addressPoint?.EmailAddress))
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.Failed_RecipientNotIdentified);
            return;
        }

        await CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.New);
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
        // set to new to allow new iteration of regular proceessing if transient error
        if (sendOperationResult.SendResult == EmailNotificationResultType.Failed_TransientError)
        {
            sendOperationResult.SendResult = EmailNotificationResultType.New;
        }

        await _repository.UpdateSendStatus(sendOperationResult.NotificationId, (EmailNotificationResultType)sendOperationResult.SendResult!, sendOperationResult.OperationId);
    }

    private async Task CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, EmailRecipient recipient, EmailNotificationResultType result)
    {
        var emailNotification = new EmailNotification()
        {
            Id = _guid.NewGuid(),
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = recipient,
            SendResult = new(result, _dateTime.UtcNow())
        };

        DateTime expiry;

        if (result == EmailNotificationResultType.Failed_RecipientNotIdentified)
        {
            expiry = _dateTime.UtcNow();
        }
        else
        {
            // lets see how much time it takes to get a status for communication services
            expiry = requestedSendTime.AddHours(1);
        }

        await _repository.AddNotification(emailNotification, expiry);
    }
}
