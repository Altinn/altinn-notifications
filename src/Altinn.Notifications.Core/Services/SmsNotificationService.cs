using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="ISmsNotificationService"/>
/// </summary>
public class SmsNotificationService : ISmsNotificationService
{
    private readonly IGuidService _guid;
    private readonly IDateTimeService _dateTime;
    private readonly ISmsNotificationRepository _repository;
    private readonly IKafkaProducer _producer;
    private readonly string _smsQueueTopicName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationService"/> class.
    /// </summary>
    public SmsNotificationService(
        IGuidService guid,
        IDateTimeService dateTime,
        ISmsNotificationRepository repository,
        IKafkaProducer producer,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _guid = guid;
        _dateTime = dateTime;
        _repository = repository;
        _producer = producer;
        _smsQueueTopicName = kafkaSettings.Value.SmsQueTopicName;
    }

    /// <inheritdoc/>
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient)
    {
        SmsAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Sms) as SmsAddressPoint;

        if (!string.IsNullOrEmpty(addressPoint?.MobileNumber))
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, recipient.RecipientId, addressPoint!.MobileNumber, SmsNotificationResultType.New);
        }
        else
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, recipient.RecipientId, string.Empty, SmsNotificationResultType.Failed_RecipientNotIdentified);
        }
    }

    /// <inheritdoc/>
    public async Task SendNotifications()
    {
        List<Sms> smsList = await _repository.GetNewNotifications();

        foreach (Sms sms in smsList)
        {
            bool success = await _producer.ProduceAsync(_smsQueueTopicName, sms.Serialize());
            if (!success)
            {
                await _repository.UpdateSendStatus(sms.NotificationId, SmsNotificationResultType.New);
            }
        }
    }

    private async Task CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, string recipientId, string recipientNumber, SmsNotificationResultType type)
    {
        var smsNotification = new SmsNotification()
        {
            Id = _guid.NewGuid(),
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            RecipientNumber = recipientNumber,
            RecipientId = string.IsNullOrEmpty(recipientId) ? null : recipientId,
            SendResult = new(type, _dateTime.UtcNow())
        };

        await _repository.AddNotification(smsNotification, requestedSendTime.AddHours(1));
    }
}
