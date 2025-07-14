using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
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
        DateTime currentTime = _dateTime.UtcNow();

        var lookupResult = await GetRecipientLookupResult(orderRequest.Recipients, orderRequest.NotificationChannel, orderRequest.ResourceId);

        var templates = SetSenderIfNotDefined(orderRequest.Templates);

        var order = new NotificationOrder
        {
            Id = orderId,
            SendersReference = orderRequest.SendersReference,
            Templates = templates,
            RequestedSendTime = orderRequest.RequestedSendTime ?? currentTime,
            NotificationChannel = orderRequest.NotificationChannel,
            Creator = orderRequest.Creator,
            Created = currentTime,
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
    public async Task<Result<NotificationOrderChainResponse, ServiceError>> RegisterNotificationOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default)
    {
        // 1. Get the current time
        DateTime currentTime = _dateTime.UtcNow();

        // 2. Early cancellation if someone’s already cancelled
        cancellationToken.ThrowIfCancellationRequested();

        // 3. Create the main order
        var mainOrderResult = await CreateMainNotificationOrderAsync(orderRequest, currentTime, cancellationToken);
        if (mainOrderResult.IsError && mainOrderResult.Error != null)
        {
            return mainOrderResult.Error;
        }

        // 4. Create reminders
        var remindersResult = await CreateReminderNotificationOrdersAsync(orderRequest.Reminders, orderRequest.Creator, currentTime, cancellationToken);
        if (remindersResult.IsError && remindersResult.Error != null)
        {
            return remindersResult.Error;
        }

        // 5. Create the response
        return await CreateChainResponseAsync(orderRequest, mainOrderResult.Value, remindersResult.Value, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationOrder, ServiceError>> RegisterInstantNotificationOrder(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default)
    {
        // 1. Get the current time
        DateTime currentTime = _dateTime.UtcNow();

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
    public async Task<InstantNotificationOrderTracking?> RetrieveInstantNotificationOrderTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetInstantOrderTracking(creatorName, idempotencyId, cancellationToken) ?? null;
    }

    /// <summary>
    /// Creates the primary <see cref="NotificationOrder"/> for a notification chain by processing
    /// recipient information, validating contact details, and configuring message templates.
    /// </summary>
    /// <param name="orderRequest">
    /// The incoming chain request containing recipient information, templates, and other notification parameters.
    /// </param>
    /// <param name="currentTime">
    /// The UTC timestamp to set as the creation time of the notification order.
    /// </param>
    private NotificationOrder CreateMainNotificationOrderAsync(InstantNotificationOrder orderRequest, DateTime currentTime)
    {
        var smsDetails = orderRequest.InstantNotificationRecipient.ShortMessageDeliveryDetails;
        var smsContent = smsDetails.ShortMessageContent;

        var smsTemplate = new SmsTemplate(smsContent.Sender, smsContent.Message);

        var smsRecipient = new Recipient([new SmsAddressPoint(smsDetails.PhoneNumber)]);

        var recipients = new List<Recipient> { smsRecipient };
        var templates = new List<INotificationTemplate> { smsTemplate };

        templates = SetSenderIfNotDefined(templates);

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

    /// <summary>
    /// Creates the primary <see cref="NotificationOrder"/> for a notification chain by processing
    /// recipient information, validating contact details, and configuring message templates.
    /// </summary>
    /// <param name="orderRequest">
    /// The incoming chain request containing recipient information, templates, and other notification parameters.
    /// </param>
    /// <param name="currentTime">
    /// The UTC timestamp to set as the creation time of the notification order.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to request cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="Result{TValue,TError}"/> containing either:
    /// <list type="bullet">
    /// <item>
    /// <description>A successful result with <see cref="Result{TValue,TError}.Value"/> containing the fully configured
    /// <see cref="NotificationOrder"/> ready for persistence and processing</description>
    /// </item>
    /// <item>
    /// <description>A failed result with <see cref="Result{TValue,TError}.Error"/> containing a
    /// <see cref="ServiceError"/> with status code and detailed error message about the failure reason</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method performs recipient validation, template preparation, and order creation:
    /// <list type="number">
    /// <item><description>Extracts delivery components from the recipient configuration</description></item>
    /// <item><description>Validates recipient contact information through lookup services</description></item>
    /// <item><description>Applies default sender information where needed</description></item>
    /// <item><description>Constructs a complete notification order with all required properties</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    private async Task<Result<NotificationOrder, ServiceError>> CreateMainNotificationOrderAsync(NotificationOrderChainRequest orderRequest, DateTime currentTime, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (recipients, templates, channel, ignoreReservation, resourceId, sendingTimePolicyForSms) = ExtractDeliveryComponents(orderRequest.Recipient);

        var lookupResult = await GetRecipientLookupResult(recipients, channel, GetSanitizedResourceId(resourceId));

        if (lookupResult?.MissingContact?.Count > 0)
        {
            return new ServiceError(422, $"Missing contact information for recipient(s): {string.Join(", ", lookupResult.MissingContact)}");
        }

        templates = SetSenderIfNotDefined(templates);

        return new NotificationOrder
        {
            Created = currentTime,
            Templates = templates,
            ResourceId = resourceId,
            Recipients = recipients,
            Type = orderRequest.Type,
            Id = orderRequest.OrderId,
            NotificationChannel = channel,
            Creator = orderRequest.Creator,
            IgnoreReservation = ignoreReservation,
            SendingTimePolicy = sendingTimePolicyForSms,
            SendersReference = orderRequest.SendersReference,
            RequestedSendTime = orderRequest.RequestedSendTime,
            ConditionEndpoint = orderRequest.ConditionEndpoint
        };
    }

    /// <summary>
    /// Creates notification orders for each reminder in a list, validating contact information
    /// for recipients and configuring message templates for each reminder.
    /// </summary>
    /// <param name="notificationReminders">
    /// The list of notification reminders to process and convert into notification orders.
    /// </param>
    /// <param name="creator">
    /// The identity of the entity that created the notification order chain.
    /// </param>
    /// <param name="currentTime">
    /// The UTC timestamp to set as the creation time for all generated reminder orders.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to request cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="Result{TValue,TError}"/> containing either:
    /// <list type="bullet">
    /// <item>
    /// <description>A successful result with <see cref="Result{TValue,TError}.Value"/> containing a list of 
    /// fully configured <see cref="NotificationOrder"/> objects ready for persistence</description>
    /// </item>
    /// <item>
    /// <description>A failed result with <see cref="Result{TValue,TError}.Error"/> containing a
    /// <see cref="ServiceError"/> with details about what went wrong during reminder processing</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// For each reminder, this method:
    /// <list type="number">
    /// <item><description>Extracts recipient contact details and notification channel preferences</description></item>
    /// <item><description>Validates recipient contact information through lookup services</description></item>
    /// <item><description>Applies default sender information where needed</description></item>
    /// <item><description>Creates a properly configured notification order with creator metadata</description></item>
    /// </list>
    /// 
    /// Processing stops at the first reminder that fails validation, returning the corresponding error.
    /// If the input list is empty or null, an empty list is returned.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    private async Task<Result<List<NotificationOrder>, ServiceError>> CreateReminderNotificationOrdersAsync(List<NotificationReminder>? notificationReminders, Creator creator, DateTime currentTime, CancellationToken cancellationToken)
    {
        var reminders = new List<NotificationOrder>();
        if (notificationReminders is not { Count: > 0 })
        {
            return reminders;
        }

        foreach (var notificationReminder in notificationReminders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (recipients, templates, channel, ignoreReservation, resourceId, sendingTimePolicyForSms) = ExtractDeliveryComponents(notificationReminder.Recipient);

            var lookupResult = await GetRecipientLookupResult(recipients, channel, GetSanitizedResourceId(resourceId));

            if (lookupResult?.MissingContact?.Count > 0)
            {
                return new ServiceError(422, $"Missing contact information for recipient(s): {string.Join(", ", lookupResult.MissingContact)}");
            }

            templates = SetSenderIfNotDefined(templates);

            reminders.Add(new NotificationOrder
            {
                Creator = creator,
                Templates = templates,
                Created = currentTime,
                Recipients = recipients,
                ResourceId = resourceId,
                NotificationChannel = channel,
                Type = notificationReminder.Type,
                Id = notificationReminder.OrderId,
                IgnoreReservation = ignoreReservation,
                SendingTimePolicy = sendingTimePolicyForSms,
                SendersReference = notificationReminder.SendersReference,
                RequestedSendTime = notificationReminder.RequestedSendTime,
                ConditionEndpoint = notificationReminder.ConditionEndpoint
            });
        }

        return reminders;
    }

    /// <summary>
    /// Persists a notification order chain and builds a response containing tracking information.
    /// </summary>
    /// <param name="orderRequest">
    /// The original chain request containing configuration details and identifiers for the notification sequence.
    /// </param>
    /// <param name="mainOrder">
    /// The primary notification order that should be persisted first and delivered immediately or at the requested time.
    /// </param>
    /// <param name="reminderOrders">
    /// Optional collection of follow-up notification orders to be delivered after the main notification.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to request cancellation of the asynchronous database operation.
    /// </param>
    /// <returns>
    /// A <see cref="Result{TValue,TError}"/> containing either:
    /// <list type="bullet">
    /// <item>
    /// <description>A successful result with <see cref="Result{TValue,TError}.Value"/> containing a 
    /// <see cref="NotificationOrderChainResponse"/> with the chain identifier and receipt information</description>
    /// </item>
    /// <item>
    /// <description>A failed result with <see cref="Result{TValue,TError}.Error"/> containing a
    /// <see cref="ServiceError"/> if persistence fails</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method:
    /// <list type="number">
    /// <item><description>Persists all orders in the chain as a single atomic transaction</description></item>
    /// <item><description>Constructs a receipt containing identifiers for both main and reminder shipments</description></item>
    /// <item><description>Returns a structured response that clients can use to track the notification chain</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    private async Task<Result<NotificationOrderChainResponse, ServiceError>> CreateChainResponseAsync(NotificationOrderChainRequest orderRequest, NotificationOrder? mainOrder, List<NotificationOrder>? reminderOrders, CancellationToken cancellationToken)
    {
        var savedOrders = new List<NotificationOrder>();
        if (mainOrder != null)
        {
            savedOrders = await _repository.Create(orderRequest, mainOrder, reminderOrders, cancellationToken);
        }

        if (savedOrders == null || savedOrders.Count == 0)
        {
            return new ServiceError(500, "Failed to create the notification order chain.");
        }

        // The first is the main shipment
        var savedMain = savedOrders[0];

        // Build response
        var response = new NotificationOrderChainResponse
        {
            OrderChainId = orderRequest.OrderChainId,
            OrderChainReceipt = new NotificationOrderChainReceipt
            {
                ShipmentId = savedMain.Id,
                SendersReference = savedMain.SendersReference,
                Reminders = savedOrders.Count > 1
                    ? [.. savedOrders
                        .Where(o => o.Id != savedMain.Id)
                        .Select(o => new NotificationOrderChainShipment
                        {
                            ShipmentId = o.Id,
                            SendersReference = o.SendersReference
                        })]
                    : null
            }
        };

        return response;
    }

    private static string? GetSanitizedResourceId(string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        return resourceId.StartsWith("urn:altinn:resource:", StringComparison.Ordinal) ? resourceId["urn:altinn:resource:".Length..] : resourceId;
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

    /// <summary>
    /// Retrieves a list of identifiers for recipients who are missing the required contact information
    /// for the specified notification channel.
    /// </summary>
    /// <param name="channel">
    /// The <see cref="NotificationChannel"/> to check for missing contact information.
    /// Supported channels include Email, SMS, EmailAndSms, EmailPreferred, and SmsPreferred.
    /// </param>
    /// <param name="recipients">
    /// A list of <see cref="Recipient"/> objects to evaluate for missing contact points.
    /// Each recipient is checked for the presence of contact information relevant to the specified channel.
    /// </param>
    /// <returns>
    /// A list of strings representing the identifiers (either <see cref="Recipient.OrganizationNumber"/> or
    /// <see cref="Recipient.NationalIdentityNumber"/>) of recipients who are missing the required contact information.
    /// </returns>
    private static List<string> GetMissingContactListIds(NotificationChannel channel, List<Recipient> recipients)
    {
        return channel switch
        {
            NotificationChannel.Email =>
                                [.. recipients
                                    .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))
                                    .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber!)],

            NotificationChannel.Sms =>
                                [.. recipients
                                    .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
                                    .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber!)],

            NotificationChannel.EmailAndSms or
            NotificationChannel.EmailPreferred or
            NotificationChannel.SmsPreferred =>
                                [.. recipients
                                    .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))
                                    .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber!)],

            _ => [],
        };
    }

    /// <summary>
    /// Retrieves a list of recipients who are missing contact information for the specified notification channel.
    /// </summary>
    /// <param name="channel">
    /// The <see cref="NotificationChannel"/> to check for missing contact information.
    /// Supported channels include SMS, Email, EmailAndSms, EmailPreferred, and SmsPreferred.
    /// </param>
    /// <param name="recipients">
    /// A list of <see cref="Recipient"/> objects to evaluate for missing contact points.
    /// </param>
    /// <returns>
    /// A list of <see cref="Recipient"/> objects that are missing the required contact information
    /// for the specified notification channel.
    /// </returns>
    /// <remarks>
    /// This method performs a deep copy of the recipients that are missing contact information to ensure the original list remains unaltered.
    /// </remarks>
    private static List<Recipient> GetMissingContactRecipientList(NotificationChannel channel, List<Recipient> recipients)
    {
        return channel switch
        {
            NotificationChannel.Sms =>
                [..recipients
                .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
                .Select(r => r.DeepCopy())],

            NotificationChannel.Email =>
                [..recipients
                .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))
                .Select(r => r.DeepCopy())],

            NotificationChannel.EmailAndSms or
            NotificationChannel.SmsPreferred or
            NotificationChannel.EmailPreferred =>
                [..recipients
                .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))
                .Select(r => r.DeepCopy())],

            _ => []
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
    /// <item><description>SmsSendingTimePolicy - The sendingTimePolicy associated with the selected SMS's configuration</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method processes different recipient types (SMS, Email, Person, Organization) and creates
    /// the appropriate templates and addressing information based on the recipient's configuration.
    /// The default channel is SMS if the recipient type cannot be determined.
    /// </remarks>
    private static (List<Recipient> Recipients, List<INotificationTemplate> Templates, NotificationChannel Channel, bool? IgnoreReservation, string? ResourceId, SendingTimePolicy? SmsSendingTimePolicy) ExtractDeliveryComponents(NotificationRecipient recipient)
    {
        bool? ignoreReservation = null;
        string? resourceIdentifier = null;

        var recipients = new List<Recipient>();
        var templates = new List<INotificationTemplate>();

        SendingTimePolicy? smsSendingTimePolicy = null;
        NotificationChannel notificationChannel = NotificationChannel.Sms;

        if (recipient.RecipientSms?.Settings != null)
        {
            notificationChannel = NotificationChannel.Sms;

            smsSendingTimePolicy = recipient.RecipientSms.Settings.SendingTimePolicy;

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
            resourceIdentifier = recipient.RecipientPerson.ResourceId;
            notificationChannel = recipient.RecipientPerson.ChannelSchema;
            ignoreReservation = recipient.RecipientPerson.IgnoreReservation;

            if (recipient.RecipientPerson.SmsSettings != null)
            {
                templates.Add(CreateSmsTemplate(recipient.RecipientPerson.SmsSettings));
                smsSendingTimePolicy = recipient.RecipientPerson.SmsSettings.SendingTimePolicy;
            }

            if (recipient.RecipientPerson.EmailSettings != null)
            {
                templates.Add(CreateEmailTemplate(recipient.RecipientPerson.EmailSettings));
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
                smsSendingTimePolicy = recipient.RecipientOrganization.SmsSettings.SendingTimePolicy;
            }

            if (recipient.RecipientOrganization.EmailSettings != null)
            {
                templates.Add(CreateEmailTemplate(recipient.RecipientOrganization.EmailSettings));
            }

            recipients.Add(new Recipient([], organizationNumber: recipient.RecipientOrganization.OrgNumber));
        }

        return (recipients, templates, notificationChannel, ignoreReservation, resourceIdentifier, smsSendingTimePolicy);
    }
}
