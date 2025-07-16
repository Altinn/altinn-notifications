using System.Web;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Provides the concrete implementation of <see cref="IInstantOrderRequestService"/> for registering and tracking instant notification orders.
/// </summary>
internal class InstantOrderRequestService : IInstantOrderRequestService
{
    private readonly string _defaultSmsSender;
    private readonly IGuidService _guidService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IOrderRepository _orderRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRequestService"/> class.
    /// </summary>
    public InstantOrderRequestService(
        IGuidService guidService,
        IDateTimeService dateTimeService,
        IOrderRepository instantOrderRepository,
        IOptions<NotificationConfig> configurationOptions)
    {
        _guidService = guidService;
        _dateTimeService = dateTimeService;
        _orderRepository = instantOrderRepository;
        _defaultSmsSender = configurationOptions.Value.DefaultSmsSenderNumber;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _orderRepository.RetrieveTrackingInformation(creatorName, idempotencyId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default)
    {
        var deliveryDetails = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails;
        var messageContent = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent;

        int messagesCount = CalculateNumberOfMessages(messageContent.Message);
        var expirationDateTime = instantNotificationOrder.Created.AddSeconds(deliveryDetails.TimeToLiveInSeconds);
        var senderIdentifier = string.IsNullOrWhiteSpace(messageContent.Sender) ? _defaultSmsSender : messageContent.Sender;

        var notificationOrder = new NotificationOrder
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

        var smsNotification = new SmsNotification()
        {
            Id = _guidService.NewGuid(),
            Recipient = new SmsRecipient()
            {
                MobileNumber = deliveryDetails.PhoneNumber
            },
            OrderId = instantNotificationOrder.OrderId,
            RequestedSendTime = instantNotificationOrder.Created,
            SendResult = new(SmsNotificationResultType.New, _dateTimeService.UtcNow())
        };

        return await _orderRepository.PersistInstantSmsNotificationAsync(instantNotificationOrder, notificationOrder, smsNotification, expirationDateTime, messagesCount, cancellationToken);
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
}
