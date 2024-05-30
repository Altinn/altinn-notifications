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
        _smsQueueTopicName = kafkaSettings.Value.SmsQueueTopicName;
    }

    /// <inheritdoc/>
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient, int smsCount, bool ignoreReservation = false)
    {
        List<SmsAddressPoint> smsAddresses = recipient.AddressInfo
          .Where(a => a.AddressType == AddressType.Sms)
          .Select(a => (a as SmsAddressPoint)!)
          .Where(a => a != null && !string.IsNullOrEmpty(a.MobileNumber))
          .ToList();

        SmsRecipient smsRecipient = new()
        {
            OrganizationNumber = recipient.OrganizationNumber,
            NationalIdentityNumber = recipient.NationalIdentityNumber,
            IsReserved = recipient.IsReserved
        };

        if (recipient.IsReserved.HasValue && recipient.IsReserved.Value && !ignoreReservation)
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, smsRecipient, SmsNotificationResultType.Failed_RecipientReserved);
            return;
        }
        else if (smsAddresses.Count == 0)
        {
            await CreateNotificationForRecipient(orderId, requestedSendTime, smsRecipient, SmsNotificationResultType.Failed_RecipientNotIdentified);
            return;
        }

        foreach (SmsAddressPoint addressPoint in smsAddresses)
        {
            smsRecipient.MobileNumber = addressPoint.MobileNumber;
            await CreateNotificationForRecipient(orderId, requestedSendTime, smsRecipient, SmsNotificationResultType.New, smsCount);
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

    /// <inheritdoc/>
    public async Task UpdateSendStatus(SmsSendOperationResult sendOperationResult)
    {
        await _repository.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference);
    }

    private async Task CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, SmsRecipient recipient, SmsNotificationResultType type, int smsCount = 0)
    {
        var smsNotification = new SmsNotification()
        {
            Id = _guid.NewGuid(),
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = recipient,
            SendResult = new(type, _dateTime.UtcNow())
        };

        await _repository.AddNotification(smsNotification, requestedSendTime.AddHours(1), smsCount);
    }
}
