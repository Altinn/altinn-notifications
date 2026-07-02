using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
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
/// <remarks>
/// Initializes a new instance of the <see cref="ComposedEmailOrderRequestService"/> class.
/// </remarks>
public class ComposedEmailOrderRequestService(
    IDateTimeService dateTime,
    IOrderRepository repository,
    IOptions<NotificationConfig> config) : IComposedEmailOrderRequestService
{
    private readonly IDateTimeService _dateTime = dateTime;
    private readonly IOrderRepository _repository = repository;
    private readonly string _defaultEmailFromAddress = config.Value.DefaultEmailFromAddress;

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
        var composedEmailSettings = composedEmail.Settings;

        var fromAddress = string.IsNullOrWhiteSpace(composedEmailSettings.SenderEmailAddress)
            ? _defaultEmailFromAddress
            : composedEmailSettings.SenderEmailAddress;

        var mainOrder = new NotificationOrder
        {
            Created = currentTime,
            Id = orderRequest.OrderId,
            Type = OrderType.Composed,
            Creator = orderRequest.Creator,
            NotificationChannel = NotificationChannel.Email,
            SendersReference = orderRequest.SendersReference,
            RequestedSendTime = orderRequest.RequestedSendTime,
            ConditionEndpoint = orderRequest.ConditionEndpoint,
            EmailAttachments = composedEmailSettings.Attachments,
            Recipients = [new([new EmailAddressPoint(composedEmail.EmailAddress)])],
            Templates = [new EmailTemplate(fromAddress, composedEmailSettings.Subject, composedEmailSettings.Body, composedEmailSettings.ContentType)]
        };

        var savedOrders = await _repository.Create(orderRequest, mainOrder, null, cancellationToken);
        var savedMainNotificationOrder = savedOrders.FirstOrDefault();
        if (savedMainNotificationOrder == null)
        {
            throw new InvalidOperationException("Could not create the notification order");
        }

        return new NotificationOrderChainResponse
        {
            OrderChainId = orderRequest.OrderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = savedMainNotificationOrder.Id,
                SendersReference = savedMainNotificationOrder.SendersReference
            }
        };
    }
}
