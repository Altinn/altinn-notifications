using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IInstantOrderRequestService"/>.
/// </summary>
internal class InstantOrderRequestService : IInstantOrderRequestService
{
    private readonly string _defaultSmsSender;
    private readonly IOrderRepository _repository;
    private readonly IDateTimeService _dateTimeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRequestService"/> class.
    /// </summary>
    public InstantOrderRequestService(IOrderRepository repository, IDateTimeService dateTimeService, IOptions<NotificationConfig> configurationOptions)
    {
        _repository = repository;
        _dateTimeService = dateTimeService;
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
        var savedInstantNotificationOrder = await _repository.Create(instantNotificationOrder, notificationOrder, cancellationToken);
        if (savedInstantNotificationOrder == null)
        {
            return new ServiceError(500, "Failed to create the instant notification order.");
        }

        return notificationOrder;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> RetrieveInstantOrderTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetInstantOrderTracking(creatorName, idempotencyId, cancellationToken) ?? null;
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
}
