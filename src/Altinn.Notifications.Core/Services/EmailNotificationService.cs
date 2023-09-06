using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Repository.Interfaces;
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
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient)
    {
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        if (!string.IsNullOrEmpty(addressPoint?.EmailAddress))
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, recipient.RecipientId, addressPoint.EmailAddress, EmailNotificationResultType.New);
        }
        else
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, recipient.RecipientId, string.Empty, EmailNotificationResultType.Failed_RecipientNotIdentified);
        }
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
                await _repository.SetResultStatus(email.NotificationId, EmailNotificationResultType.New);
            }
        }
    }

    /// <inheritdoc/>
    public async Task UpdateStatusNotification(SendOperationResult sendOperationResult)
    {
        await _repository.SetResultStatus(sendOperationResult.NotificationId, (EmailNotificationResultType)sendOperationResult.SendResult!);
    }

    private async Task CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, string recipientId, string toAddress, EmailNotificationResultType result)
    {
        var emailNotification = new EmailNotification()
        {
            Id = _guid.NewGuid(),
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            ToAddress = toAddress,
            RecipientId = string.IsNullOrEmpty(recipientId) ? null : recipientId,
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