using System;
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
    private readonly IDateTimeService _dateTimeService;
    private readonly IInstantOrderRepository _instantOrderRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRequestService"/> class.
    /// </summary>
    public InstantOrderRequestService(
        IDateTimeService dateTimeService,
        IInstantOrderRepository instantOrderRepository,
        IOptions<NotificationConfig> configurationOptions)
    {
        _dateTimeService = dateTimeService;
        _instantOrderRepository = instantOrderRepository;
        _defaultSmsSender = configurationOptions.Value.DefaultSmsSenderNumber;
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationOrder, ServiceError>> RegisterInstantOrder(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default)
    {
        // 1. Get the current time
        DateTime currentTime = _dateTimeService.UtcNow();

        // 2. Early cancellation if someone’s already cancelled
        cancellationToken.ThrowIfCancellationRequested();

        // 3. Instantiate the main order
        var notificationOrder = CreateMainNotificationOrderAsync(instantNotificationOrder, currentTime);

        // 4. Inserts the instant notification order and the instantiated notification order into the database
        var savedInstantNotificationOrder = await _instantOrderRepository.Create(instantNotificationOrder, notificationOrder, cancellationToken);
        if (savedInstantNotificationOrder == null)
        {
            return new ServiceError(500, "Failed to create the instant notification order.");
        }

        return notificationOrder;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> RetrieveInstantOrderTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _instantOrderRepository.GetInstantOrderTracking(creatorName, idempotencyId, cancellationToken) ?? null;
    }

    /// <summary>
    /// Creates a <see cref="NotificationOrder"/> for an instant notification by processing
    /// recipient details and configuring the SMS message template.
    /// </summary>
    /// <param name="orderRequest">The instant notification order containing recipient and message details.</param>
    /// <param name="currentTime">The UTC timestamp to set as the creation time of the notification order.</param>
    /// <returns>A fully configured <see cref="NotificationOrder"/> ready for persistence and processing.</returns>
    private NotificationOrder CreateMainNotificationOrderAsync(InstantNotificationOrder orderRequest, DateTime currentTime)
    {
        var smsDetails = orderRequest.InstantNotificationRecipient.ShortMessageDeliveryDetails;
        var smsContent = smsDetails.ShortMessageContent;

        var smsTemplate = new SmsTemplate(smsContent.Sender, smsContent.Message);

        var smsRecipient = new Recipient([new SmsAddressPoint(smsDetails.PhoneNumber)]);

        var templates = SetSenderIfNotDefined([smsTemplate]);
        var recipients = new List<Recipient> { smsRecipient };

        return new NotificationOrder
        {
            ResourceId = null,
            Created = currentTime,
            Templates = templates,
            Recipients = recipients,
            IgnoreReservation = null,
            Type = orderRequest.Type,
            ConditionEndpoint = null,
            Id = orderRequest.OrderId,
            Creator = orderRequest.Creator,
            RequestedSendTime = currentTime,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Sms,
            SendersReference = orderRequest.SendersReference
        };
    }

    private List<INotificationTemplate> SetSenderIfNotDefined(List<INotificationTemplate> templates)
    {
        foreach (var template in templates.OfType<SmsTemplate>().Where(e => string.IsNullOrEmpty(e.SenderNumber)))
        {
            template.SenderNumber = _defaultSmsSender;
        }

        return templates;
    }

    private static int GetSmsCountForOrder(NotificationOrder order)
    {
        SmsTemplate? smsTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Sms) as SmsTemplate;
        return CalculateNumberOfMessages(smsTemplate!.Body);
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

    private async Task ProcessInstantOrder(NotificationOrder order, int timeToLiveInSeconds, CancellationToken cancellationToken = default)
    {
        var recipient = order.Recipients.First(e => e.AddressInfo.Exists(e => e.AddressType == AddressType.Sms));

        var addressPoint = recipient.AddressInfo.OfType<SmsAddressPoint>().First();

        int smsCount = GetSmsCountForOrder(order);

        var smsRecipient = new SmsRecipient()
        {
            MobileNumber = addressPoint.MobileNumber
        };

        var expiryDateTime = order.RequestedSendTime.AddSeconds(timeToLiveInSeconds);

        //await _smsService.CreateNotificationAsync(order.Id, order.RequestedSendTime, smsRecipient, expiryDateTime, smsCount, cancellationToken);
    }

    private async Task CreateNotificationAsync(Guid orderId, DateTime requestedSendTime, SmsRecipient recipient, DateTime expiryDateTime, int smsCount, CancellationToken cancellationToken = default)
    {
        //var smsNotification = new SmsNotification()
        //{
        //    OrderId = orderId,
        //    Id = _guid.NewGuid(),
        //    Recipient = recipient,
        //    RequestedSendTime = requestedSendTime,
        //    SendResult = new(SmsNotificationResultType.New, _dateTime.UtcNow())
        //};

        //await _repository.AddNotification(smsNotification, expiryDateTime, smsCount, cancellationToken);
    }

}
