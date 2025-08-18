using System.Web;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implements the <see cref="IInstantOrderRequestService"/> interface
/// to handle the registration, persistence, and tracking of instant notification orders for SMS delivery.
/// </summary>
public class InstantOrderRequestService : IInstantOrderRequestService
{
    private readonly string _defaultSmsSender;
    private readonly IGuidService _guidService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IOrderRepository _orderRepository;
    private readonly IShortMessageServiceClient _shortMessageServiceClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrderRequestService"/> class.
    /// </summary>
    public InstantOrderRequestService(
        IGuidService guidService,
        IDateTimeService dateTimeService,
        IOrderRepository instantOrderRepository,
        IOptions<NotificationConfig> configurationOptions,
        IShortMessageServiceClient shortMessageServiceClient)
    {
        _guidService = guidService;
        _dateTimeService = dateTimeService;
        _orderRepository = instantOrderRepository;
        _shortMessageServiceClient = shortMessageServiceClient;
        _defaultSmsSender = configurationOptions.Value.DefaultSmsSenderNumber;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _orderRepository.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deliveryDetails = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails;
        var messageContent = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent;

        var messagesCount = CalculateNumberOfMessages(messageContent.Message);

        var smsNotification = CreateSmsNotification(instantNotificationOrder, deliveryDetails);

        var notificationOrder = CreateNotificationOrder(instantNotificationOrder, deliveryDetails, messageContent);

        var expirationDateTime = notificationOrder.RequestedSendTime.AddSeconds(deliveryDetails.TimeToLiveInSeconds);

        cancellationToken.ThrowIfCancellationRequested();

        // Create the tracking information for the order.
        var trackingInformation = await _orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, expirationDateTime, messagesCount, cancellationToken);
        if (trackingInformation != null)
        {
            _ = Task.Run(
                async () =>
                {
                    var shortMessage = new ShortMessage
                    {
                        Message = messageContent.Message,
                        NotificationId = smsNotification.Id,
                        TimeToLive = deliveryDetails.TimeToLiveInSeconds,
                        Recipient = smsNotification.Recipient.MobileNumber,
                        Sender = string.IsNullOrWhiteSpace(messageContent.Sender) ? _defaultSmsSender : messageContent.Sender
                    };

                    await _shortMessageServiceClient.SendAsync(shortMessage);
                },
                CancellationToken.None);
        }

        return trackingInformation;
    }

    /// <summary>
    /// Calculates the number of SMS messages required to deliver the specified message,
    /// applying SMS gateway concatenation rules:
    /// - Messages up to 160 characters are sent as a single SMS.
    /// - Longer messages are split into segments of 134 characters each (concatenated SMS).
    /// - The total number of segments is capped at 16, per gateway limitations.
    /// The calculation uses the URL-encoded length of the message to account for special characters.
    /// </summary>
    private static int CalculateNumberOfMessages(string message)
    {
        const int maxCharactersPerMessage = 160;
        const int maxMessagesPerConcatenation = 16;
        const int charactersPerConcatenatedMessage = 134;

        string urlEncodedMessage = HttpUtility.UrlEncode(message);
        int messageLength = urlEncodedMessage.Length;

        if (messageLength <= maxCharactersPerMessage)
        {
            return 1;
        }

        // Calculate the number of messages for messages exceeding 160 characters
        int numberOfMessages = (int)Math.Ceiling((double)messageLength / charactersPerConcatenatedMessage);

        // Check if the total number of messages exceeds the limit
        if (numberOfMessages > maxMessagesPerConcatenation)
        {
            numberOfMessages = maxMessagesPerConcatenation;
        }

        return numberOfMessages;
    }

    /// <summary>
    /// Creates a <see cref="SmsNotification"/> instance for an instant SMS notification.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="ShortMessageDeliveryDetails"/> containing the recipient's phone number.
    /// </param>
    /// <returns>
    /// A <see cref="SmsNotification"/> object populated for SMS delivery.
    /// </returns>
    private SmsNotification CreateSmsNotification(InstantNotificationOrder instantNotificationOrder, ShortMessageDeliveryDetails deliveryDetails)
    {
        return new SmsNotification
        {
            Id = _guidService.NewGuid(),
            Recipient = new SmsRecipient
            {
                MobileNumber = deliveryDetails.PhoneNumber
            },
            OrderId = instantNotificationOrder.OrderId,
            RequestedSendTime = instantNotificationOrder.Created,
            SendResult = new(SmsNotificationResultType.Sending, _dateTimeService.UtcNow())
        };
    }

    /// <summary>
    /// Creates a <see cref="NotificationOrder"/> instance for an instant SMS notification.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="ShortMessageDeliveryDetails"/> containing recipient phone number and time-to-live.
    /// </param>
    /// <param name="messageContent">
    /// The <see cref="ShortMessageContent"/> containing the SMS message body and sender information.
    /// </param>
    /// <returns>
    /// A <see cref="NotificationOrder"/> object populated for SMS delivery.
    /// </returns>
    private NotificationOrder CreateNotificationOrder(InstantNotificationOrder instantNotificationOrder, ShortMessageDeliveryDetails deliveryDetails, ShortMessageContent messageContent)
    {
        var senderIdentifier = string.IsNullOrWhiteSpace(messageContent.Sender) ? _defaultSmsSender : messageContent.Sender;

        return new NotificationOrder
        {
            ResourceId = null,
            IgnoreReservation = null,
            ConditionEndpoint = null,
            Type = instantNotificationOrder.Type,
            Id = instantNotificationOrder.OrderId,
            Creator = instantNotificationOrder.Creator,
            Created = instantNotificationOrder.Created,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = instantNotificationOrder.Created,
            SendersReference = instantNotificationOrder.SendersReference,
            Recipients = [new([new SmsAddressPoint(deliveryDetails.PhoneNumber)])],
            Templates = [new SmsTemplate(senderIdentifier, messageContent.Message)]
        };
    }
}
