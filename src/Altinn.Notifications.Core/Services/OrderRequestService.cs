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
    public async Task<NotificationOrderRequestResponse> RegisterNotificationOrderChain(NotificationOrderChainRequest orderRequest)
    {
        DateTime currentTime = _dateTime.UtcNow();

        var mainOrder = await CreateNotificationOrder(
            orderRequest.Recipient,
            orderRequest.OrderId,
            orderRequest.SendersReference,
            orderRequest.RequestedSendTime,
            orderRequest.Creator,
            currentTime,
            orderRequest.ConditionEndpoint);

        List<NotificationOrder> reminderOrders = await CreateReminderOrders(orderRequest.Reminders, orderRequest.Creator, currentTime);

        List<NotificationOrder> savedOrders = await _repository.Create(orderRequest, mainOrder, reminderOrders);

        return new NotificationOrderRequestResponse
        {
        };
    }

    /// <summary>
    /// Creates a notification order based on passed information.
    /// </summary>
    private async Task<NotificationOrder> CreateNotificationOrder(NotificationRecipient recipient, Guid orderId, string? sendersReference, DateTime requestedSendTime, Creator creator, DateTime currentTime, Uri? conditionEndpoint)
    {
        var (recipients, templates, notificationChannel, ignoreReservation, resourceIdentifier) = ExtractDeliveryComponents(recipient);

        var lookupResult = await GetRecipientLookupResult(recipients, notificationChannel, resourceIdentifier);
        if (lookupResult?.MissingContact?.Count > 0)
        {
            // TODO: Reject order due to missing contact information
        }

        templates = SetSenderIfNotDefined(templates);

        return new NotificationOrder(
            orderId,
            sendersReference,
            templates,
            requestedSendTime,
            notificationChannel,
            creator,
            currentTime,
            recipients,
            ignoreReservation,
            resourceIdentifier,
            conditionEndpoint);
    }

    /// <summary>
    /// Creates notification orders for reminders if any exist.
    /// </summary>
    private async Task<List<NotificationOrder>> CreateReminderOrders(List<NotificationReminder>? reminders, Creator creator, DateTime currentTime)
    {
        List<NotificationOrder> reminderOrders = [];
        if (reminders == null || reminders.Count == 0)
        {
            return reminderOrders;
        }

        foreach (var reminder in reminders)
        {
            var reminderOrder = await CreateNotificationOrder(
                reminder.Recipient,
                reminder.OrderId,
                reminder.SendersReference,
                reminder.RequestedSendTime,
                creator,
                currentTime,
                reminder.ConditionEndpoint);

            reminderOrders.Add(reminderOrder);
        }

        return reminderOrders;
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

    /// <summary>
    /// Adds templates for person notifications based on available settings.
    /// </summary>
    private static void AddTemplatesForPerson(RecipientPerson person, List<INotificationTemplate> templates)
    {
        if (person.SmsSettings != null)
        {
            templates.Add(CreateSmsTemplate(person.SmsSettings));
        }

        if (person.EmailSettings != null)
        {
            templates.Add(CreateEmailTemplate(person.EmailSettings));
        }
    }

    /// <summary>
    /// Adds templates for organization notifications based on available settings.
    /// </summary>
    private static void AddTemplatesForOrganization(RecipientOrganization organization, List<INotificationTemplate> templates)
    {
        if (organization.SmsSettings != null)
        {
            templates.Add(CreateSmsTemplate(organization.SmsSettings));
        }

        if (organization.EmailSettings != null)
        {
            templates.Add(CreateEmailTemplate(organization.EmailSettings));
        }
    }

    /// <summary>
    /// Creates an Email template from Email settings.
    /// </summary>
    private static EmailTemplate CreateEmailTemplate(EmailSendingOptions emailSettings)
    {
        return new EmailTemplate(
            emailSettings.SenderEmailAddress,
            emailSettings.Subject,
            emailSettings.Body,
            emailSettings.ContentType);
    }

    /// <summary>
    /// Creates an SMS template from SMS settings.
    /// </summary>
    private static SmsTemplate CreateSmsTemplate(SmsSendingOptions smsSettings)
    {
        return new SmsTemplate(smsSettings.Sender, smsSettings.Body);
    }

    /// <summary>
    /// Extracts information from a <see cref="NotificationRecipient"/> into notification delivery components.
    /// </summary>
    /// <param name="recipient">The notification recipient containing targeting and messaging preferences.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><description>Recipients - A list of recipients with proper addressing information</description></item>
    /// <item><description>Templates - Notification templates based on the recipient's configuration</description></item>
    /// <item><description>NotificationChannel - The determined notification channel based on recipient type</description></item>
    /// <item><description>IgnoreReservation - Flag indicating whether to bypass KRR reservations</description></item>
    /// <item><description>ResourceIdentifier - Optional resource ID for authorization and tracking</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method processes different recipient types (SMS, Email, Person, Organization) and creates
    /// the appropriate templates and addressing information based on the recipient's configuration.
    /// The default channel is SMS if the recipient type cannot be determined.
    /// </remarks>
    private static (List<Recipient> Recipients, List<INotificationTemplate> Templates, NotificationChannel NotificationChannel, bool? IgnoreReservation, string? ResourceIdentifier) ExtractDeliveryComponents(NotificationRecipient recipient)
    {
        // Initialize default values
        bool? ignoreReservation = null;
        List<Recipient> recipients = [];
        string? resourceIdentifier = null;
        List<INotificationTemplate> templates = [];
        NotificationChannel notificationChannel = NotificationChannel.Sms;

        if (recipient.RecipientSms?.Settings != null)
        {
            notificationChannel = NotificationChannel.Sms;
            templates.Add(CreateSmsTemplate(recipient.RecipientSms.Settings));
            recipients.Add(new Recipient([new SmsAddressPoint(recipient.RecipientSms.PhoneNumber)]));
        }
        else if (recipient.RecipientEmail?.Settings != null)
        {
            notificationChannel = NotificationChannel.Email;
            templates.Add(CreateEmailTemplate(recipient.RecipientEmail.Settings));
            recipients.Add(new Recipient([new EmailAddressPoint(recipient.RecipientEmail.EmailAddress)]));
        }
        else if (recipient.RecipientPerson != null)
        {
            notificationChannel = recipient.RecipientPerson.ChannelSchema;
            resourceIdentifier = recipient.RecipientPerson.ResourceId;
            ignoreReservation = recipient.RecipientPerson.IgnoreReservation;
            recipients.Add(new Recipient([], nationalIdentityNumber: recipient.RecipientPerson.NationalIdentityNumber));

            AddTemplatesForPerson(recipient.RecipientPerson, templates);
        }
        else if (recipient.RecipientOrganization != null)
        {
            notificationChannel = recipient.RecipientOrganization.ChannelSchema;
            resourceIdentifier = recipient.RecipientOrganization.ResourceId;
            recipients.Add(new Recipient([], organizationNumber: recipient.RecipientOrganization.OrgNumber));

            AddTemplatesForOrganization(recipient.RecipientOrganization, templates);
        }

        return (recipients, templates, notificationChannel, ignoreReservation, resourceIdentifier);
    }
}
