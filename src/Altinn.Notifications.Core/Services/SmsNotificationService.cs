using System.Diagnostics;

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
    private readonly int _publishBatchSize;
    private readonly IKafkaProducer _producer;
    private readonly string _smsQueueTopicName;
    private readonly IDateTimeService _dateTime;
    private readonly ISmsNotificationRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationService"/> class.
    /// </summary>
    public SmsNotificationService(
        IGuidService guid,
        IKafkaProducer producer,
        IDateTimeService dateTime,
        ISmsNotificationRepository repository,
        IOptions<KafkaSettings> kafkaSettings,
        IOptions<NotificationConfig> notificationConfig)
    {
        _guid = guid;
        _dateTime = dateTime;
        _producer = producer;
        _repository = repository;
        _smsQueueTopicName = kafkaSettings.Value.SmsQueueTopicName;

        var configuredBatchSize = notificationConfig.Value.SmsPublishBatchSize;
        _publishBatchSize = configuredBatchSize > 0 ? configuredBatchSize : 50;
    }

    /// <inheritdoc/>
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, DateTime expiryDateTime, List<SmsAddressPoint> addressPoints, SmsRecipient recipient, int count, bool ignoreReservation = false)
    {
        if (recipient.IsReserved.HasValue && recipient.IsReserved.Value && !ignoreReservation)
        {
            await CreateNotification(orderId, requestedSendTime, expiryDateTime, recipient, SmsNotificationResultType.Failed_RecipientReserved);
            return;
        }

        if (addressPoints.Count == 0)
        {
            await CreateNotification(orderId, requestedSendTime, expiryDateTime, recipient, SmsNotificationResultType.Failed_RecipientNotIdentified);
            return;
        }

        foreach (SmsAddressPoint addressPoint in addressPoints)
        {
            recipient.MobileNumber = addressPoint.MobileNumber;
            await CreateNotification(orderId, requestedSendTime, expiryDateTime, recipient, SmsNotificationResultType.New, count);
        }
    }

    /// <inheritdoc/>
    public async Task SendNotifications(CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime)
    {
        List<Sms> newSmsNotifications;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            newSmsNotifications = await _repository.GetNewNotifications(_publishBatchSize, cancellationToken, sendingTimePolicy);

            foreach (var newSmsNotification in newSmsNotifications)
            {
                try
                {
                    var success = await _producer.ProduceAsync(_smsQueueTopicName, newSmsNotification.Serialize());
                    if (!success)
                    {
                        await _repository.UpdateSendStatus(newSmsNotification.NotificationId, SmsNotificationResultType.New);
                    }
                }
                catch (Exception)
                {
                    await _repository.UpdateSendStatus(newSmsNotification.NotificationId, SmsNotificationResultType.New);
                }
            }
        }
        while (newSmsNotifications.Count == _publishBatchSize);
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
    /// Creates a new SMS notification and adds it to the repository.
    /// </summary>
    /// <param name="orderId">The unique identifier of the order associated with the notification.</param>
    /// <param name="requestedSendTime">The time at which the notification is requested to be sent.</param>
    /// <param name="expiryDateTime">The date and time when the notification expires and should no longer be sent.</param>
    /// <param name="recipient">The recipient details for the SMS notification.</param>
    /// <param name="resultType">The result type indicating the status of the notification.</param>
    /// <param name="count">The number of attempts made to send the notification. Defaults to 0.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CreateNotification(Guid orderId, DateTime requestedSendTime, DateTime expiryDateTime, SmsRecipient recipient, SmsNotificationResultType resultType, int count = 0)
    {
        var smsNotification = new SmsNotification()
        {
            OrderId = orderId,
            Id = _guid.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(resultType, _dateTime.UtcNow())
        };

        await _repository.AddNotification(smsNotification, expiryDateTime, count);
    }
}
