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
/// Implementation of <see cref="ISmsNotificationService"/>
/// </summary>
public class SmsNotificationService : ISmsNotificationService
{
    private readonly IGuidService _guidService;
    private readonly int _publishBatchSize;
    private readonly string _smsQueueTopicName;
    private readonly IDateTimeService _dateTimeService;
    private readonly ISmsNotificationRepository _repository;
    private readonly ISendSmsPublisher _smsPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationService"/> class.
    /// </summary>
    public SmsNotificationService(
        IGuidService guidService,
        IKafkaProducer producer,
        IDateTimeService dateTimeService,
        ISmsNotificationRepository repository,
        ISendSmsPublisher smsPublisher,
        IOptions<KafkaSettings> kafkaSettings,
        IOptions<NotificationConfig> notificationConfig)
    {
        _guidService = guidService;
        _dateTimeService = dateTimeService;
        _repository = repository;
        _smsPublisher = smsPublisher;
        _smsQueueTopicName = kafkaSettings.Value.SmsQueueTopicName;

        var configuredPublishBatchSize = notificationConfig.Value.SmsPublishBatchSize;
        _publishBatchSize = configuredPublishBatchSize > 0 ? configuredPublishBatchSize : 500;
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
            newSmsNotifications = [];

            try
            {
                newSmsNotifications = await _repository.GetNewNotifications(_publishBatchSize, cancellationToken, sendingTimePolicy);
                if (newSmsNotifications.Count == 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Publish(newSmsNotifications, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await ResetSendStatusToNewAsync(newSmsNotifications);

                throw;
            }
            catch (InvalidOperationException)
            {
                await ResetSendStatusToNewAsync(newSmsNotifications);

                throw;
            }
        }
        while (newSmsNotifications.Count > 0);
    }

    /// <summary>
    /// Publishes a collection of SMS notifications to a Kafka topic asynchronously and updates their send status based
    /// on the publishing result.
    /// </summary>
    /// <remarks>After attempting to publish each SMS notification, the method updates the send status in the
    /// repository for notifications that were not successfully published. The operation is performed asynchronously and
    /// can be cancelled using the provided cancellation token.</remarks>
    /// <param name="newSmsNotifications">A list of SMS notifications to be published. Each notification must be properly serialized before being sent to
    /// the Kafka topic.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the publish operation.</param>
    /// <returns>A task that represents the asynchronous publish operation. The task does not return a value.</returns>
    private async Task Publish(List<Sms> newSmsNotifications, CancellationToken cancellationToken)
    {
        var unpublishedSmsNotifications = await _smsPublisher.PublishAsync([.. newSmsNotifications], cancellationToken);

        foreach (var unpublishedSmsNotification in unpublishedSmsNotifications)
        {
            if (unpublishedSmsNotification == null || unpublishedSmsNotification.NotificationId == Guid.Empty)
            {
                continue;
            }

            await _repository.UpdateSendStatus(unpublishedSmsNotification.NotificationId, SmsNotificationResultType.New);
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
            Id = _guidService.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(resultType, _dateTimeService.UtcNow())
        };

        await _repository.AddNotification(smsNotification, expiryDateTime, count);
    }

    /// <summary>
    /// Resets the send status to <see cref="SmsNotificationResultType.New"/> for the given SMS notifications.
    /// </summary>
    /// <param name="smsNotifications">The collection of SMS notifications to reset the send status for.</param>
    private async Task ResetSendStatusToNewAsync(IEnumerable<Sms> smsNotifications)
    {
        if (smsNotifications is null)
        {
            return;
        }

        foreach (var sms in smsNotifications)
        {
            await _repository.UpdateSendStatus(sms.NotificationId, SmsNotificationResultType.New);
        }
    }
}
