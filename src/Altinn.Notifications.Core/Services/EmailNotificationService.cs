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
    private readonly IDateTimeService _dateTime;
    private readonly IGuidService _guid;
    private readonly string _emailQueueTopicName;
    private readonly IEmailNotificationRepository _repository;
    private readonly IOrderRepository _orderRepository;
    private readonly IKafkaProducer _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationService"/> class.
    /// </summary>
    public EmailNotificationService(
        IGuidService guid,
        IDateTimeService dateTime,
        IEmailNotificationRepository repository,
        IKafkaProducer producer,
        IOptions<KafkaSettings> kafkaSettings,
        IOrderRepository orderRepository)
    {
        _guid = guid;
        _dateTime = dateTime;
        _producer = producer;
        _repository = repository;
        _orderRepository = orderRepository;
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

        var emailDeliveryStatus = (EmailNotificationResultType)sendOperationResult.SendResult!;

        await _repository.UpdateSendStatus(sendOperationResult.NotificationId, emailDeliveryStatus, sendOperationResult.OperationId);

        switch (emailDeliveryStatus)
        {
            case EmailNotificationResultType.New:
            case EmailNotificationResultType.Sending:
            case EmailNotificationResultType.Succeeded:
            case EmailNotificationResultType.Failed_TransientError:
                break;

            case EmailNotificationResultType.Failed:
            case EmailNotificationResultType.Delivered:
            case EmailNotificationResultType.Failed_Bounced:
            case EmailNotificationResultType.Failed_Quarantined:
            case EmailNotificationResultType.Failed_FilteredSpam:
            case EmailNotificationResultType.Failed_RecipientReserved:
            case EmailNotificationResultType.Failed_InvalidEmailFormat:
            case EmailNotificationResultType.Failed_SupressedRecipient:
            case EmailNotificationResultType.Failed_RecipientNotIdentified:
                await _orderRepository.TryMarkOrderAsCompleted(sendOperationResult.NotificationId, AlternateIdentifierSource.Email);
                break;
        }
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
            expiry = requestedSendTime.AddHours(1);
        }

        await _repository.AddNotification(emailNotification, expiry);
    }
}
