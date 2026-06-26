using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implements <see cref="IComposedEmailOrderRequestService"/> to handle registration
/// and tracking of composed email notification orders.
/// </summary>
public class ComposedEmailOrderRequestService : IComposedEmailOrderRequestService
{
    private readonly string _defaultEmailFromAddress;
    private readonly IDateTimeService _dateTime;
    private readonly IGuidService _guid;
    private readonly IOrderRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComposedEmailOrderRequestService"/> class.
    /// </summary>
    public ComposedEmailOrderRequestService(
        IOrderRepository repository,
        IGuidService guid,
        IDateTimeService dateTime,
        IOptions<NotificationConfig> config)
    {
        _guid = guid;
        _dateTime = dateTime;
        _repository = repository;
        _defaultEmailFromAddress = config.Value.DefaultEmailFromAddress;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderChainResponse?> RetrieveOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetComposedOrderChainTracking(creatorName, idempotencyId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderChainResponse> RegisterComposedEmailOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTime currentTime = _dateTime.UtcNow();

        var composedEmail = orderRequest.Recipient.RecipientComposedEmail!;
        var settings = composedEmail.Settings;

        var fromAddress = string.IsNullOrWhiteSpace(settings.SenderEmailAddress)
            ? _defaultEmailFromAddress
            : settings.SenderEmailAddress;

        var mainOrder = new NotificationOrder
        {
            Created = currentTime,
            Id = orderRequest.OrderId,
            Type = OrderType.Composed,
            Creator = orderRequest.Creator,
            EmailAttachments = settings.Attachments,
            NotificationChannel = NotificationChannel.Email,
            SendersReference = orderRequest.SendersReference,
            RequestedSendTime = orderRequest.RequestedSendTime,
            ConditionEndpoint = orderRequest.ConditionEndpoint,
            Recipients = [new([new EmailAddressPoint(composedEmail.EmailAddress)])],
            Templates = [new EmailTemplate(fromAddress, settings.Subject, settings.Body, settings.ContentType)]
        };

        var savedOrders = await _repository.Create(orderRequest, mainOrder, null, cancellationToken);
        var savedMain = savedOrders[0];

        return new NotificationOrderChainResponse
        {
            OrderChainId = orderRequest.OrderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = savedMain.Id,
                SendersReference = savedMain.SendersReference,
                Reminders = null
            }
        };
    }
}
