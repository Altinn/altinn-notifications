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
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, List<SmsAddressPoint> addressPoints, SmsRecipient recipient, int count, bool ignoreReservation = false)
    {
        if (recipient.IsReserved.HasValue && recipient.IsReserved.Value && !ignoreReservation)
        {
            await CreateNotification(orderId, requestedSendTime, recipient, SmsNotificationResultType.Failed_RecipientReserved);
            return;
        }

        if (addressPoints.Count == 0)
        {
            await CreateNotification(orderId, requestedSendTime, recipient, SmsNotificationResultType.Failed_RecipientNotIdentified);
            return;
        }

        foreach (SmsAddressPoint addressPoint in addressPoints)
        {
            recipient.MobileNumber = addressPoint.MobileNumber;
            await CreateNotification(orderId, requestedSendTime, recipient, SmsNotificationResultType.New, count);
        }
    }

    /// <inheritdoc/>
    public async Task CreateNotificationAsync(Guid orderId, DateTime requestedSendTime, SmsRecipient recipient, DateTime expiryDateTime, int smsCount, CancellationToken cancellationToken = default)
    {
        var smsNotification = new SmsNotification()
        {
            OrderId = orderId,
            Id = _guid.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(SmsNotificationResultType.New, _dateTime.UtcNow())
        };

        await _repository.AddNotification(smsNotification, expiryDateTime, smsCount, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendNotifications(SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime)
    {
        var smsList = await _repository.GetNewNotifications(sendingTimePolicy);
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
    public async Task TerminateExpiredNotifications()
    {
        await _repository.TerminateExpiredNotifications();
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(SmsSendOperationResult sendOperationResult)
    {
        await _repository.UpdateSendStatus(sendOperationResult.NotificationId, sendOperationResult.SendResult, sendOperationResult.GatewayReference);
    }

    /// <summary>
    /// Creates a new SMS notification for a specific recipient.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order associated with the notification.</param>
    /// <param name="requestedSendTime">The date and time when the notification is requested to be sent.</param>
    /// <param name="recipient">The recipient details of the SMS notification.</param>
    /// <param name="resultType">The result type of the SMS notification.</param>
    /// <param name="count">The number of SMS messages to be sent. Default is 0.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CreateNotification(Guid orderId, DateTime requestedSendTime, SmsRecipient recipient, SmsNotificationResultType resultType, int count = 0)
    {
        var smsNotification = new SmsNotification()
        {
            OrderId = orderId,
            Id = _guid.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(resultType, _dateTime.UtcNow())
        };

        await _repository.AddNotification(smsNotification, requestedSendTime.AddHours(48), count);
    }
}
