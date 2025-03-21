using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
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

    /// <inheritdoc/>
    public async Task<NotificationOrderRequestResponse> RegisterNotificationOrder(NotificationOrderSequenceRequest orderRequest)
    {
        throw new NotImplementedException();
    }

    private async Task<List<RecipientLookupResult?>> GetRecipientLookupResult(NotificationOrderSequenceRequest orderRequest)
    {
        throw new NotImplementedException();
        
        //var recipientsByChannel = new Dictionary<NotificationChannel, List<ResourceBoundRecipients>>
        //{
        //    { NotificationChannel.Sms, new List<ResourceBoundRecipients>() },
        //    { NotificationChannel.Email, new List<ResourceBoundRecipients>() },
        //    { NotificationChannel.SmsPreferred, new List<ResourceBoundRecipients>() },
        //    { NotificationChannel.EmailPreferred, new List<ResourceBoundRecipients>() }
        //};

        //// Organize the contacts  from the main order based on communication channel and resource identifier.
        //OrganizeRecipientsByChannelAndResource(orderRequest.Recipient, recipientsByChannel);

        //// Organize the contacts from associated reminders order based on communication channel and resource identifier.
        //if (orderRequest.Reminders?.Count > 0)
        //{
        //    foreach (var reminder in orderRequest.Reminders)
        //    {
        //        OrganizeRecipientsByChannelAndResource(reminder.Recipient, recipientsByChannel);
        //    }
        //}

        //// Get the contact points for the recipients and return the list of recipients that are missing contact points.
        //var recipientLookupResults = new List<RecipientLookupResult?>();
        //foreach (var (channel, resourceBoundRecipientsList) in recipientsByChannel)
        //{
        //    foreach (var resourceBoundRecipients in resourceBoundRecipientsList)
        //    {
        //        var lookupResult = await GetRecipientLookupResult(resourceBoundRecipients.Recipients, channel, resourceBoundRecipients.ResourceId);
        //        recipientLookupResults.Add(lookupResult);
        //    }
        //}

        //return recipientLookupResults;
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
            NotificationChannel.Email => [.. recipients
                               .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))
                               .Select(r => r.DeepCopy())],
            NotificationChannel.Sms => [.. recipients
                               .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
                               .Select(r => r.DeepCopy())],
            NotificationChannel.EmailPreferred or NotificationChannel.SmsPreferred => [.. recipients
                               .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))
                               .Select(r => r.DeepCopy())],
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

    private NotificationOrderSequenceRequest SetSenderIfNotDefined(NotificationOrderSequenceRequest orderRequest)
    {
        throw new NotImplementedException();

        //// Apply sender defaults to main recipient
        //ApplyDefaultSender(orderRequest.Recipient);

        //// Apply sender defaults to reminders
        //if (orderRequest.Reminders != null)
        //{
        //    foreach (var reminder in orderRequest.Reminders)
        //    {
        //        ApplyDefaultSender(reminder.Recipient);

        //        reminder.RequestedSendTime = orderRequest.RequestedSendTime.AddDays(reminder.DelayDays ?? 0);
        //    }
        //}

        //return orderRequest;
    }

    /// <summary>
    /// Applies default sender values if they are not set.
    /// </summary>
    private void ApplyDefaultSender(RecipientSpecification recipients)
    {
        if (recipients == null)
        {
            return;
        }

        ApplyDefaultSenderToPerson(recipients.RecipientPerson);
        ApplyDefaultSenderToOrganization(recipients.RecipientOrganization);
    }

    /// <summary>
    /// Sets default sender values for a person recipient if the settings exist but values are null.
    /// </summary>
    private void ApplyDefaultSenderToPerson(RecipientPerson? person)
    {
        if (person?.SmsSettings != null && string.IsNullOrEmpty(person.SmsSettings.Sender))
        {
            person.SmsSettings.Sender = _defaultSmsSender;
        }
        else if (person?.EmailSettings != null && string.IsNullOrEmpty(person.EmailSettings.SenderEmailAddress))
        {
            person.EmailSettings.SenderEmailAddress = _defaultEmailFromAddress;
        }
    }

    /// <summary>
    /// Sets default sender values for an organization recipient if the settings exist but values are null.
    /// </summary>
    private void ApplyDefaultSenderToOrganization(RecipientOrganization? organization)
    {
        if (organization?.SmsSettings != null && string.IsNullOrEmpty(organization.SmsSettings.Sender))
        {
            organization.SmsSettings.Sender = _defaultSmsSender;
        }
        else if (organization?.EmailSettings != null && string.IsNullOrEmpty(organization.EmailSettings.SenderEmailAddress))
        {
            organization.EmailSettings.SenderEmailAddress = _defaultEmailFromAddress;
        }
    }

    /// <summary>
    /// Organizes recipients from an <see cref="RecipientSpecification"/> container into appropriate notification channels while preserving their resource context.
    /// </summary>
    /// <param name="recipientDetails">Container with recipient information, which may include person and/or organization recipients.</param>
    /// <param name="recipientsByChannel">Dictionary that categorizes recipients by notification channel and resource identifier.</param>
    /// <remarks>
    /// This method extracts recipients from the provided container and organizes them into channel-specific collections:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Processes <see cref="RecipientPerson"/> recipients, mapping them to their specified channel.</description>
    ///   </item>
    ///   <item>
    ///     <description>Processes <see cref="RecipientOrganization"/> recipients, mapping them to their specified channel.</description>
    ///   </item>
    /// </list>
    /// Recipients are grouped by both notification channel and resource identifier, enabling efficient channel-specific 
    /// processing while maintaining their association with resources. Each recipient is wrapped in a 
    /// <see cref="ResourceBoundRecipients"/> container before being added to the appropriate channel collection.
    /// </remarks>
    private static void OrganizeRecipientsByChannelAndResource(RecipientSpecification? recipientDetails, Dictionary<NotificationChannel, List<ResourceBoundRecipients>> recipientsByChannel)
    {
        if (recipientDetails is null)
        {
            return;
        }

        if (recipientDetails.RecipientPerson is not null)
        {
            AddResourceBoundRecipientsToChannel(
                recipientDetails.RecipientPerson.ChannelSchema,
                new ResourceBoundRecipients { Recipients = [new Recipient() { NationalIdentityNumber = recipientDetails.RecipientPerson.NationalIdentityNumber }], ResourceId = recipientDetails.RecipientPerson.ResourceId },
                recipientsByChannel);
        }

        if (recipientDetails.RecipientOrganization is not null)
        {
            AddResourceBoundRecipientsToChannel(
                recipientDetails.RecipientOrganization.ChannelSchema,
                new ResourceBoundRecipients { Recipients = [new Recipient() { NationalIdentityNumber = recipientDetails.RecipientOrganization.OrgNumber }], ResourceId = recipientDetails.RecipientOrganization.ResourceId },
                recipientsByChannel);
        }
    }

    /// <summary>
    /// Adds or merges resource-bound recipients to the appropriate notification channel collection.
    /// </summary>
    /// <param name="channel">The notification channel (Email, SMS, EmailPreferred, or SmsPreferred) to which the recipients should be added.</param>
    /// <param name="resourceBoundRecipient">A group of recipients associated with a specific resource identifier.</param>
    /// <param name="resourceBoundRecipients">A dictionary organizing recipients by notification channel and resource identifier.</param>
    /// <remarks>
    /// This method performs one of two operations:
    /// <list type="bullet">
    ///   <item>
    ///     <description>If recipients for the specified resource ID already mapped to the channel, it merges the new recipients with the existing group.</description>
    ///   </item>
    ///   <item>
    ///     <description>If no recipients exist for the resource ID, it maps the new resource-bound recipients group to the channel.</description>
    ///   </item>
    /// </list>
    /// This grouping by resource ID enables efficient lookup and processing of recipients that share the same resource context.
    /// </remarks>
    private static void AddResourceBoundRecipientsToChannel(NotificationChannel channel, ResourceBoundRecipients resourceBoundRecipient, Dictionary<NotificationChannel, List<ResourceBoundRecipients>> resourceBoundRecipients)
    {
        var channelRecipients = resourceBoundRecipients[channel];

        var existingRecipientsGroup = channelRecipients.FirstOrDefault(r => r.ResourceId == resourceBoundRecipient.ResourceId);
        if (existingRecipientsGroup is not null)
        {
            existingRecipientsGroup.Recipients.AddRange(resourceBoundRecipient.Recipients);
        }
        else
        {
            channelRecipients.Add(resourceBoundRecipient);
        }
    }
}
