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

        var order = new NotificationOrder
        {
            Id = orderId,
            SendersReference = orderRequest.SendersReference,
            Templates = templates,
            RequestedSendTime = orderRequest.RequestedSendTime ?? currentime,
            NotificationChannel = orderRequest.NotificationChannel,
            Creator = orderRequest.Creator,
            Created = currentime,
            Recipients = orderRequest.Recipients,
            IgnoreReservation = orderRequest.IgnoreReservation,
            ResourceId = orderRequest.ResourceId,
            ConditionEndpoint = orderRequest.ConditionEndpoint
        };

        NotificationOrder savedOrder = await _repository.Create(order);

        return new NotificationOrderRequestResponse()
        {
            OrderId = savedOrder.Id,
            RecipientLookup = lookupResult
        };
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderChainResponse?> RetrieveOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetOrderChainTracking(creatorName, idempotencyId, cancellationToken) ?? null;
    }

    /// <inheritdoc/>
    public async Task<NotificationOrderChainResponse> RegisterNotificationOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default)
    {
        DateTime currentTime = _dateTime.UtcNow();

        cancellationToken.ThrowIfCancellationRequested();

        var mainOrder = await CreateNotificationOrder(
            orderRequest.Recipient,
            orderRequest.OrderId,
            orderRequest.SendersReference,
            orderRequest.RequestedSendTime,
            orderRequest.Creator,
            currentTime,
            orderRequest.ConditionEndpoint);

        var reminderOrders = await CreateNotificationOrders(
            orderRequest.Reminders,
            orderRequest.Creator,
            currentTime,
            cancellationToken);

        List<NotificationOrder> savedOrders = await _repository.Create(orderRequest, mainOrder, reminderOrders, cancellationToken);

        if (savedOrders == null || savedOrders.Count == 0)
        {
            throw new InvalidOperationException("Failed to create the notification order chain.");
        }

        // Get the main order (first in the list)
        var savedMainOrder = savedOrders[0];

        // Create and return the response
        return new NotificationOrderChainResponse
        {
            OrderChainId = orderRequest.OrderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = savedMainOrder.Id,
                SendersReference = savedMainOrder.SendersReference,
                Reminders = savedOrders.Count > 1
                    ? [.. savedOrders.Skip(1)
                        .Select(order => new NotificationOrderChainShipment
                        {
                            ShipmentId = order.Id,
                            SendersReference = order.SendersReference
                        })]
                    : null
            }
        };
    }

    /// <summary>
    /// Creates a new notification order using the specified recipient details and order parameters.
    /// </summary>
    /// <param name="recipient">
    /// The <see cref="NotificationRecipient"/> that contains all necessary details for delivering the notification.
    /// </param>
    /// <param name="orderId">
    /// A <see cref="Guid"/> that uniquely identifies the notification order.
    /// </param>
    /// <param name="sendersReference">
    /// An optional reference identifier provided by the sender for correlating the order with external systems.
    /// </param>
    /// <param name="requestedSendTime">
    /// The desired date and time for delivering the notification. If not specified, the current UTC date and time is used.
    /// </param>
    /// <param name="creator">
    /// The creator information encapsulated in a <see cref="Creator"/> object that identifies who initiated the order.
    /// </param>
    /// <param name="currentTime">
    /// The current UTC date and time marking when the order is created.
    /// </param>
    /// <param name="conditionEndpoint">
    /// An optional <see cref="Uri"/> that serves as an endpoint to evaluate whether the notification should be sent.
    /// </param>
    /// <returns>
    /// A <see cref="Task{NotificationOrder}"/> representing the asynchronous operation that returns the newly created notification order.
    /// </returns>
    /// <remarks>
    /// This method extracts the delivery components (such as the recipient list, notification templates,
    /// notification channel, reservation flag, and resource identifier) from the provided <paramref name="recipient"/>.
    /// It performs a recipient lookup to ensure all necessary contact information exists.
    /// If any required contact data is missing, an <see cref="InvalidOperationException"/> is thrown.
    /// Additionally, default sender information is applied to any templates that lack sender details.
    /// </remarks>
    private async Task<NotificationOrder> CreateNotificationOrder(NotificationRecipient recipient, Guid orderId, string? sendersReference, DateTime requestedSendTime, Creator creator, DateTime currentTime, Uri? conditionEndpoint)
    {
        var (recipients, templates, channel, ignoreReservation, resourceId, sendingTimePolicy) = ExtractDeliveryComponents(recipient);

        var lookupResult = await GetRecipientLookupResult(recipients, channel, resourceId);
        if (lookupResult?.MissingContact?.Count > 0)
        {
            throw new InvalidOperationException($"Missing contact information for recipient(s): {string.Join(", ", lookupResult.MissingContact)}");
        }

        templates = SetSenderIfNotDefined(templates);

        return new NotificationOrder
        {
            Id = orderId,
            SendersReference = sendersReference,
            Templates = templates,
            RequestedSendTime = requestedSendTime,
            NotificationChannel = channel,
            Creator = creator,
            Created = currentTime,
            Recipients = recipients,
            IgnoreReservation = ignoreReservation,
            ResourceId = resourceId,
            ConditionEndpoint = conditionEndpoint,
            SendingTimePolicy = sendingTimePolicy
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

            case NotificationChannel.EmailAndSms:
                await _contactPointService.AddEmailAndSmsContactPointsAsync(recipientsWithoutContactPoint, resourceId);
                break;

            case NotificationChannel.SmsPreferred:
            case NotificationChannel.EmailPreferred:
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
        foreach (var template in templates.OfType<EmailTemplate>().Where(e => string.IsNullOrEmpty(e.FromAddress)))
        {
            template.FromAddress = _defaultEmailFromAddress;
        }

        foreach (var template in templates.OfType<SmsTemplate>().Where(e => string.IsNullOrEmpty(e.SenderNumber)))
        {
            template.SenderNumber = _defaultSmsSender;
        }

        return templates;
    }

    /// <summary>
    /// Creates an instance of <see cref="EmailTemplate"/> based on the provided Email sending options.
    /// </summary>
    private static EmailTemplate CreateEmailTemplate(EmailSendingOptions emailSettings)
    {
        return new EmailTemplate(emailSettings.SenderEmailAddress, emailSettings.Subject, emailSettings.Body, emailSettings.ContentType);
    }

    /// <summary>
    /// Creates an instance of <see cref="SmsTemplate"/> based on the provided SMS sending options.
    /// </summary>
    private static SmsTemplate CreateSmsTemplate(SmsSendingOptions smsSettings)
    {
        return new SmsTemplate(smsSettings.Sender, smsSettings.Body);
    }

    /// <summary>
    /// Creates notification orders for each reminder provided.
    /// </summary>
    /// <param name="reminders">
    /// A list of <see cref="NotificationReminder"/> objects representing the reminders to be sent after the main notification order.
    /// </param>
    /// <param name="creator">
    /// The <see cref="Creator"/> associated with the reminder orders, indicating the originator of the notifications.
    /// </param>
    /// <param name="currentTime">
    /// The current UTC date and time, used as the reference time for creating the orders.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation, yielding a <see cref="List{NotificationOrder}"/> objects representing reminder orders.
    /// </returns>
    /// <remarks>
    /// This method iterates through the provided reminders and, for each reminder, invokes 
    /// <see cref="CreateNotificationOrder"/> to generate a corresponding reminder order using the reminder's details.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    private async Task<List<NotificationOrder>> CreateNotificationOrders(List<NotificationReminder>? reminders, Creator creator, DateTime currentTime, CancellationToken cancellationToken = default)
    {
        var reminderOrders = new List<NotificationOrder>();

        if (reminders == null || reminders.Count == 0)
        {
            return reminderOrders;
        }

        foreach (var reminder in reminders)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

    /// <summary>
    /// Extracts information from a <see cref="NotificationRecipient"/> into notification delivery components.
    /// </summary>
    /// <param name="recipient">The notification recipient containing targeting and messaging preferences.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><description>Recipients - A list of recipients with proper addressing information</description></item>
    /// <item><description>Templates - Notification templates based on the recipient's configuration</description></item>
    /// <item><description>Channel - The determined notification channel based on recipient type</description></item>
    /// <item><description>IgnoreReservation - Flag indicating whether to bypass KRR reservations</description></item>
    /// <item><description>ResourceId - Optional resource ID for authorization and tracking</description></item>
    /// <item><description>SendingTimePolicy - The sendingTimePolicy associated with the selected recipient's configuration</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method processes different recipient types (SMS, Email, Person, Organization) and creates
    /// the appropriate templates and addressing information based on the recipient's configuration.
    /// The default channel is SMS if the recipient type cannot be determined.
    /// </remarks>
    private static (List<Recipient> Recipients, List<INotificationTemplate> Templates, NotificationChannel Channel, bool? IgnoreReservation, string? ResourceId, SendingTimePolicy? SendingTimePolicy) ExtractDeliveryComponents(NotificationRecipient recipient)
    {
        bool? ignoreReservation = null;
        string? resourceIdentifier = null;

        var recipients = new List<Recipient>();
        var templates = new List<INotificationTemplate>();

        NotificationChannel notificationChannel = NotificationChannel.Sms;

        SendingTimePolicy? sendingTimePolicy = null;

        if (recipient.RecipientSms?.Settings != null)
        {
            notificationChannel = NotificationChannel.Sms;

            sendingTimePolicy = recipient.RecipientSms.Settings.SendingTimePolicy;

            templates.Add(CreateSmsTemplate(recipient.RecipientSms.Settings));

            recipients.Add(new Recipient([new SmsAddressPoint(recipient.RecipientSms.PhoneNumber)]));
        }
        else if (recipient.RecipientEmail?.Settings != null)
        {
            notificationChannel = NotificationChannel.Email;

            sendingTimePolicy = recipient.RecipientEmail.Settings.SendingTimePolicy;

            templates.Add(CreateEmailTemplate(recipient.RecipientEmail.Settings));

            recipients.Add(new Recipient([new EmailAddressPoint(recipient.RecipientEmail.EmailAddress)]));
        }
        else if (recipient.RecipientPerson != null)
        {
            resourceIdentifier = recipient.RecipientPerson.ResourceId;
            notificationChannel = recipient.RecipientPerson.ChannelSchema;
            ignoreReservation = recipient.RecipientPerson.IgnoreReservation;

            if (recipient.RecipientPerson.SmsSettings != null)
            {
                templates.Add(CreateSmsTemplate(recipient.RecipientPerson.SmsSettings));
                sendingTimePolicy = recipient.RecipientPerson.SmsSettings.SendingTimePolicy;
            }

            if (recipient.RecipientPerson.EmailSettings != null)
            {
                templates.Add(CreateEmailTemplate(recipient.RecipientPerson.EmailSettings));
                sendingTimePolicy = recipient.RecipientPerson.EmailSettings.SendingTimePolicy;
            }

            recipients.Add(new Recipient([], nationalIdentityNumber: recipient.RecipientPerson.NationalIdentityNumber));
        }
        else if (recipient.RecipientOrganization != null)
        {
            resourceIdentifier = recipient.RecipientOrganization.ResourceId;
            notificationChannel = recipient.RecipientOrganization.ChannelSchema;

            if (recipient.RecipientOrganization.SmsSettings != null)
            {
                templates.Add(CreateSmsTemplate(recipient.RecipientOrganization.SmsSettings));
                sendingTimePolicy = recipient.RecipientOrganization.SmsSettings.SendingTimePolicy;
            }

            if (recipient.RecipientOrganization.EmailSettings != null)
            {
                templates.Add(CreateEmailTemplate(recipient.RecipientOrganization.EmailSettings));
                sendingTimePolicy = recipient.RecipientOrganization.EmailSettings.SendingTimePolicy;
            }

            recipients.Add(new Recipient([], organizationNumber: recipient.RecipientOrganization.OrgNumber));
        }

        return (recipients, templates, notificationChannel, ignoreReservation, resourceIdentifier, sendingTimePolicy);
    }
}
