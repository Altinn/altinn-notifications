using System.Web;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

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
    private readonly IInstantOrderRepository _instantOrderRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRequestService"/> class.
    /// </summary>
    public InstantOrderRequestService(
        IGuidService guidService,
        IDateTimeService dateTimeService,
        IInstantOrderRepository instantOrderRepository,
        IOptions<NotificationConfig> configurationOptions)
    {
        _guidService = guidService;
        _dateTimeService = dateTimeService;
        _instantOrderRepository = instantOrderRepository;
        _defaultSmsSender = configurationOptions.Value.DefaultSmsSenderNumber;
    }

    /// <inheritdoc/>
    public async Task<Result<InstantNotificationOrderTracking, ServiceError>> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        var notificationOrderTracking = await _instantOrderRepository.RetrieveTrackingInformation(creatorName, idempotencyId, cancellationToken);
        if (notificationOrderTracking is null)
        {
            return new InstantNotificationOrderTracking()
            {
                OrderChainId = Guid.Empty,
                Notification = new NotificationOrderChainShipment
                {
                    ShipmentId = Guid.Empty,
                    SendersReference = string.Empty
                }
            };
        }

        return notificationOrderTracking;
    }

    /// <inheritdoc/>
    public async Task<Result<InstantNotificationOrderTracking, ServiceError>> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default)
    {
        DateTime currentDateTime = _dateTimeService.UtcNow();

        var smsDeliveryDetails = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails;

        var recipients = new List<Recipient> { new([new SmsAddressPoint(smsDeliveryDetails.PhoneNumber)]) };

        var smsTemplate = new SmsTemplate(smsDeliveryDetails.ShortMessageContent.Sender, smsDeliveryDetails.ShortMessageContent.Message);
        var smsTemplates = SetDefaultSender([smsTemplate]);

        var notificationOrder = new NotificationOrder
        {
            ResourceId = null,
            Recipients = recipients,
            Templates = smsTemplates,
            IgnoreReservation = null,
            ConditionEndpoint = null,
            Created = currentDateTime,
            RequestedSendTime = currentDateTime,
            Type = instantNotificationOrder.Type,
            Id = instantNotificationOrder.OrderId,
            Creator = instantNotificationOrder.Creator,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Sms,
            SendersReference = instantNotificationOrder.SendersReference
        };

        int smsMessageCount = CalculateNumberOfMessages(smsDeliveryDetails.ShortMessageContent.Message);

        var smsExpiryTime = currentDateTime.AddSeconds(instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds);

        var smsNotification = new SmsNotification()
        {
            Id = _guidService.NewGuid(),
            Recipient = new SmsRecipient()
            {
                MobileNumber = smsDeliveryDetails.PhoneNumber
            },
            RequestedSendTime = currentDateTime,
            OrderId = instantNotificationOrder.OrderId,
            SendResult = new(SmsNotificationResultType.New, _dateTimeService.UtcNow())
        };

        var savedInstantNotificationOrder = await _instantOrderRepository.PersistInstantSmsNotificationAsync(instantNotificationOrder, notificationOrder, smsNotification, smsExpiryTime, smsMessageCount, cancellationToken);
        if (savedInstantNotificationOrder == null)
        {
            return new ServiceError(500, "Failed to create the presist the instant notification order.");
        }

        return new InstantNotificationOrderTracking()
        {
            OrderChainId = instantNotificationOrder.OrderChainId,

            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = instantNotificationOrder.OrderId,
                SendersReference = instantNotificationOrder.SendersReference
            }
        };
    }

    /// <summary>
    /// Calculates the number of messages based on the rules for concatenation of SMS messages in the SMS gateway.
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
    /// Sets the default SMS sender identifier for any <see cref="SmsTemplate"/> in the list that does not have one defined.
    /// </summary>
    /// <param name="templates">The notification templates to update.</param>
    /// <returns>The updated list of notification templates.</returns>
    private List<INotificationTemplate> SetDefaultSender(List<INotificationTemplate> templates)
    {
        foreach (var template in templates.OfType<SmsTemplate>().Where(e => string.IsNullOrEmpty(e.SenderNumber)))
        {
            template.SenderNumber = _defaultSmsSender;
        }

        return templates;
    }
}
