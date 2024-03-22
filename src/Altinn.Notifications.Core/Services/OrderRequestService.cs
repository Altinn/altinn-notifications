﻿using Altinn.Notifications.Core.Configuration;
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
        IOptions<NotificationOrderConfig> config)
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
        DateTime created = _dateTime.UtcNow();

        var lookupResult = await GetRecipientLookupResult(orderRequest.Recipients, orderRequest.NotificationChannel);

        if (lookupResult.Status == RecipientLookupStatus.Failed)
        {
            return new NotificationOrderRequestResponse()
            {
                RecipientLookup = lookupResult
            };
        }

        var templates = SetSenderIfNotDefined(orderRequest.Templates);

        var order = new NotificationOrder(
            orderId,
            orderRequest.SendersReference,
            templates,
            orderRequest.RequestedSendTime,
            orderRequest.NotificationChannel,
            orderRequest.Creator,
            created,
            orderRequest.Recipients);

        NotificationOrder savedOrder = await _repository.Create(order);

        return new NotificationOrderRequestResponse()
        {
            OrderId = savedOrder.Id,
            RecipientLookup = lookupResult
        };
    }

    private async Task<RecipientLookupResult> GetRecipientLookupResult(List<Recipient> recipients, NotificationChannel channel)
    {
        List<Recipient> recipientsWithoutContactPoint = new();

        foreach (var recipient in recipients)
        {
            if (channel == NotificationChannel.Email && !recipient.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))
            {
                recipientsWithoutContactPoint.Add(recipient);
            }
            else if (channel == NotificationChannel.Sms && !recipient.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
            {
                recipientsWithoutContactPoint.Add(recipient);
            }
        }

        if (recipientsWithoutContactPoint.Count > 0)
        {
            if (channel == NotificationChannel.Email)
            {
                await _contactPointService.AddEmailContactPoints(recipientsWithoutContactPoint);
            }
            else if (channel == NotificationChannel.Sms)
            {
                await _contactPointService.AddSmsContactPoints(recipientsWithoutContactPoint);
            }
        }

        RecipientLookupResult lookupResult = new()
        {
            IsReserved = recipients.Where(r => r.IsReserved.HasValue && r.IsReserved.Value).Select(r => r.NationalIdentityNumber!).ToList(),
            MissingContact = recipients
            .Where(r => channel == NotificationChannel.Email ? 
                !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email) : 
                !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
            .Select(r => r.OrganisationNumber ?? r.NationalIdentityNumber!).ToList()
        };

        int recipientsWeCannotReach = lookupResult.MissingContact.Union(lookupResult.IsReserved).ToList().Count;

        if (recipientsWeCannotReach == recipients.Count)
        {
            lookupResult.Status = RecipientLookupStatus.Failed;
        }
        else if (recipientsWeCannotReach > 0)
        {
            lookupResult.Status = RecipientLookupStatus.PartialSuccess;
        }

        return lookupResult;
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
