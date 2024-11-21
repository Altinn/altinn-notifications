using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IOrderRequestService"/>. 
/// </summary>
public class OrderRequestService : IOrderRequestService
{
    private readonly IOrderRepository _repository;
    private readonly IContactPointService _contactPointService;
    private readonly IGuidService _guid;
    private readonly IDateTimeService _dateTime;
    private readonly string _defaultEmailFromAddress;
    private readonly string _defaultSmsSender;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRequestService"/> class.
    /// </summary>
    public OrderRequestService(
        IOrderRepository repository,
        IContactPointService contactPointService,
        IGuidService guid,
        IDateTimeService dateTime,
        IOptions<NotificationConfig> config)
    {
        _repository = repository;
        _contactPointService = contactPointService;
        _guid = guid;
        _dateTime = dateTime;
        _defaultEmailFromAddress = config.Value.DefaultEmailFromAddress;
        _defaultSmsSender = config.Value.DefaultSmsSenderNumber;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderRequestResponse> RegisterNotificationOrder(NotificationOrderRequest orderRequest)
    {
        Guid orderId = _guid.NewGuid();
        DateTime currentime = _dateTime.UtcNow();

        var lookupResult = await GetRecipientLookupResult(orderRequest.Recipients, orderRequest.NotificationChannel, orderRequest.ResourceId);

        var templates = SetSenderIfNotDefined(orderRequest.Templates);

        var order = new NotificationOrder(
            orderId,
            orderRequest.SendersReference,
            templates,
            orderRequest.RequestedSendTime ?? currentime,
            orderRequest.NotificationChannel,
            orderRequest.Creator,
            currentime,
            orderRequest.Recipients,
            orderRequest.IgnoreReservation,
            orderRequest.ResourceId,
            orderRequest.ConditionEndpoint);

        NotificationOrder savedOrder = await _repository.Create(order);

        return new NotificationOrderRequestResponse()
        {
            OrderId = savedOrder.Id,
            RecipientLookup = lookupResult
        };
    }

    private async Task<RecipientLookupResult?> GetRecipientLookupResult(List<Recipient> originalRecipients, NotificationChannel channel, string? resourceId)
    {
        List<Recipient> recipientsWithoutContactPoint = GetMissingContactRecipientList(channel, originalRecipients);

        if (recipientsWithoutContactPoint.Count == 0)
        {
            return null;
        }

        switch (channel)
        {
            case NotificationChannel.Email:
                await _contactPointService.AddEmailContactPoints(recipientsWithoutContactPoint, resourceId);
                break;
            case NotificationChannel.Sms:
                await _contactPointService.AddSmsContactPoints(recipientsWithoutContactPoint, resourceId);
                break;
            case NotificationChannel.EmailPreferred:
            case NotificationChannel.SmsPreferred:
                await _contactPointService.AddPreferredContactPoints(channel, recipientsWithoutContactPoint, resourceId);
                break;
        }

        var isReserved = recipientsWithoutContactPoint.Where(r => r.IsReserved.HasValue && r.IsReserved.Value).Select(r => r.NationalIdentityNumber!).ToList();

        RecipientLookupResult lookupResult = new()
        {
            IsReserved = isReserved,
            MissingContact = GetMissingContactListIds(channel, recipientsWithoutContactPoint).Except(isReserved).ToList()
        };

        int recipientsWeCannotReach = lookupResult.MissingContact.Union(lookupResult.IsReserved).ToList().Count;

        if (recipientsWeCannotReach == recipientsWithoutContactPoint.Count)
        {
            lookupResult.Status = RecipientLookupStatus.Failed;
        }
        else if (recipientsWeCannotReach > 0)
        {
            lookupResult.Status = RecipientLookupStatus.PartialSuccess;
        }

        return lookupResult;
    }

    private static List<string> GetMissingContactListIds(NotificationChannel channel, List<Recipient> recipients)
    {
        return channel switch
        {
            NotificationChannel.Email => recipients
                               .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))
                               .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber!)
                               .ToList(),
            NotificationChannel.Sms => recipients
                                .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
                                .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber!)
                                .ToList(),
            NotificationChannel.EmailPreferred or NotificationChannel.SmsPreferred => recipients
                              .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))
                              .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber!)
                              .ToList(),
            _ => [],
        };
    }

    private static List<Recipient> GetMissingContactRecipientList(NotificationChannel channel, List<Recipient> recipients)
    {
        return channel switch
        {
            NotificationChannel.Email => recipients
                               .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))
                               .Select(r => r.DeepCopy())
                               .ToList(),
            NotificationChannel.Sms => recipients
                                .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
                              .Select(r => r.DeepCopy())
                               .ToList(),
            NotificationChannel.EmailPreferred or NotificationChannel.SmsPreferred => recipients
                              .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))
                             .Select(r => r.DeepCopy())
                               .ToList(),
            _ => [],
        };
    }

    private List<INotificationTemplate> SetSenderIfNotDefined(List<INotificationTemplate> templates)
    {
        foreach (var template in templates.OfType<EmailTemplate>().Where(template => string.IsNullOrEmpty(template.FromAddress)))
        {
            template.FromAddress = _defaultEmailFromAddress;
        }

        foreach (var template in templates.OfType<SmsTemplate>().Where(template => string.IsNullOrEmpty(template.SenderNumber)))
        {
            template.SenderNumber = _defaultSmsSender;
        }

        return templates;
    }
}
