using Altinn.Authorization.ProblemDetails;
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
    IServiceOwnerService serviceOwnerService,
    IOptions<NotificationConfig> config) : IComposedEmailOrderRequestService
{
    private readonly IDateTimeService _dateTime = dateTime;
    private readonly IOrderRepository _repository = repository;
    private readonly IServiceOwnerService _serviceOwnerService = serviceOwnerService;
    private readonly string _defaultEmailFromAddress = config.Value.DefaultEmailFromAddress;

    /// <inheritdoc/>
    public async Task<NotificationOrderChainResponse?> RetrieveOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetComposedOrderChainTracking(creatorName, idempotencyId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationOrderChainResponse>> RegisterComposedEmailOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var composedEmail = orderRequest.Recipient.RecipientComposedEmail!;
        var composedEmailSettings = composedEmail.Settings;

        var senderProblem = _serviceOwnerService.ValidateSenders(orderRequest.Creator.ShortName, [composedEmailSettings.SenderEmailAddress], []);
        if (senderProblem != null)
        {
            return senderProblem;
        }

        DateTime currentTime = _dateTime.UtcNow();

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

        var result = await _repository.Create(orderRequest, mainOrder, null, cancellationToken);

        return new NotificationOrderChainResponse
        {
            OrderChainId = result.OrderChainId,
            IsNewlyCreated = result.IsNewlyCreated,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = result.ShipmentId,
                SendersReference = result.SendersReference
            }
        };
    }
}
