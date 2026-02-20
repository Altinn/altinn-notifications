using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Errors;
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
        DateTime currentTime = _dateTime.UtcNow();

        var lookupResult = await GetRecipientLookupResult(orderRequest.Recipients, orderRequest.NotificationChannel, orderRequest.ResourceId);

        var templates = SetSenderIfNotDefined(orderRequest.Templates);

        var order = new NotificationOrder
        {
            Id = orderId,
            Templates = templates,
            Created = currentTime,
            Creator = orderRequest.Creator,
            Recipients = orderRequest.Recipients,
            ResourceId = orderRequest.ResourceId,
            SendersReference = orderRequest.SendersReference,
            IgnoreReservation = orderRequest.IgnoreReservation,
            ConditionEndpoint = orderRequest.ConditionEndpoint,
            NotificationChannel = orderRequest.NotificationChannel,
            RequestedSendTime = orderRequest.RequestedSendTime ?? currentTime
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
    public async Task<Result<NotificationOrderChainResponse>> RegisterNotificationOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default)
    {
        // 1. Get the current time
        DateTime currentTime = _dateTime.UtcNow();

        // 2. Early cancellation if someone’s already cancelled
        cancellationToken.ThrowIfCancellationRequested();

        // 3. Create the main order
        var mainOrderResult = await CreateMainNotificationOrderAsync(orderRequest, currentTime);
        if (mainOrderResult.IsProblem)
        {
            return mainOrderResult.Problem;
        }

        // 4. Create reminders
        var remindersResult = await CreateReminderNotificationOrdersAsync(orderRequest.Reminders, orderRequest.Creator, currentTime, cancellationToken);
        if (remindersResult.IsProblem)
        {
            return remindersResult.Problem;
        }

        // 5. Create the response
        return await CreateChainResponseAsync(orderRequest, mainOrderResult.Value, remindersResult.Value, cancellationToken);
    }

    /// <summary>
    /// Creates an instance of <see cref="SmsTemplate"/> based on the provided SMS sending options.
    /// </summary>
    private static SmsTemplate CreateSmsTemplate(SmsSendingOptions smsSettings)
    {
        return new SmsTemplate(smsSettings.Sender, smsSettings.Body);
    }

    /// <summary>
    /// Creates an instance of <see cref="EmailTemplate"/> based on the provided Email sending options.
    /// </summary>
    private static EmailTemplate CreateEmailTemplate(EmailSendingOptions emailSettings)
    {
        return new EmailTemplate(emailSettings.SenderEmailAddress, emailSettings.Subject, emailSettings.Body, emailSettings.ContentType);
    }

    /// <summary>
    /// Applies default sender information to notification templates that do not have sender details configured.
    /// </summary>
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
    /// Extracts information from a <see cref="NotificationRecipient"/> into notification delivery components.
    /// </summary>
    /// <param name="recipient">The notification recipient containing targeting and messaging preferences.</param>
    /// <returns>
    /// A <see cref="RecipientDeliveryDetails"/> containing recipients, templates, channel, and other delivery settings.
    /// </returns>
    /// <remarks>
    /// This method processes different recipient types (SMS, Email, Person, Organization) and creates
    /// the appropriate templates and addressing information based on the recipient's configuration.
    /// The default channel is SMS if the recipient type cannot be determined.
    /// </remarks>
    private static RecipientDeliveryDetails ExtractDeliveryDetails(NotificationRecipient recipient)
    {
        if (recipient.RecipientSms?.Settings != null)
        {
            return ExtractSmsRecipientComponents(recipient.RecipientSms);
        }

        if (recipient.RecipientEmail?.Settings != null)
        {
            return ExtractEmailRecipientComponents(recipient.RecipientEmail);
        }

        if (recipient.RecipientPerson != null)
        {
            return ExtractPersonRecipientComponents(recipient.RecipientPerson);
        }

        if (recipient.RecipientOrganization != null)
        {
            return ExtractOrganizationRecipientComponents(recipient.RecipientOrganization);
        }

        if (recipient.RecipientExternalIdentity != null)
        {
            return ExtractExternalIdentityRecipientComponents(recipient.RecipientExternalIdentity);
        }

        return RecipientDeliveryDetails.Empty;
    }

    /// <summary>
    /// Extracts delivery components for an SMS-only recipient.
    /// </summary>
    private static RecipientDeliveryDetails ExtractSmsRecipientComponents(RecipientSms recipientSms)
    {
        return new RecipientDeliveryDetails
        {
            Channel = NotificationChannel.Sms,
            Templates = [CreateSmsTemplate(recipientSms.Settings!)],
            SmsSendingTimePolicy = recipientSms.Settings!.SendingTimePolicy,
            Recipients = [new([new SmsAddressPoint(recipientSms.PhoneNumber)])]
        };
    }

    /// <summary>
    /// Extracts delivery components for an email-only recipient.
    /// </summary>
    private static RecipientDeliveryDetails ExtractEmailRecipientComponents(RecipientEmail recipientEmail)
    {
        return new RecipientDeliveryDetails
        {
            Channel = NotificationChannel.Email,
            Templates = [CreateEmailTemplate(recipientEmail.Settings!)],
            Recipients = [new([new EmailAddressPoint(recipientEmail.EmailAddress)])]
        };
    }

    /// <summary>
    /// Extracts delivery components for a person recipient identified by national identity number.
    /// </summary>
    private static RecipientDeliveryDetails ExtractPersonRecipientComponents(RecipientPerson recipientPerson)
    {
        var (templates, smsSendingTimePolicy) = ExtractTemplatesFromSettings(recipientPerson.SmsSettings, recipientPerson.EmailSettings);

        return new RecipientDeliveryDetails
        {
            Templates = templates,
            Channel = recipientPerson.ChannelSchema,
            ResourceId = recipientPerson.ResourceId,
            SmsSendingTimePolicy = smsSendingTimePolicy,
            IgnoreReservation = recipientPerson.IgnoreReservation,
            Recipients = [new([], nationalIdentityNumber: recipientPerson.NationalIdentityNumber)]
        };
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
    /// A list of strings representing the identifiers (either <see cref="Recipient.OrganizationNumber"/>,
    /// <see cref="Recipient.NationalIdentityNumber"/>, or <see cref="Recipient.ExternalIdentity"/>) of recipients
    /// who are missing the required contact information.
    /// </returns>
    private static List<string> GetMissingContactListIds(NotificationChannel channel, List<Recipient> recipients)
    {
        return channel switch
        {
            NotificationChannel.Email =>
                                [.. recipients
                                    .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))
                                    .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber ?? r.ExternalIdentity!)],

            NotificationChannel.Sms =>
                                [.. recipients
                                    .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms))
                                    .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber ?? r.ExternalIdentity!)],

            NotificationChannel.EmailAndSms or
            NotificationChannel.EmailPreferred or
            NotificationChannel.SmsPreferred =>
                                [.. recipients
                                    .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))
                                    .Select(r => r.OrganizationNumber ?? r.NationalIdentityNumber ?? r.ExternalIdentity!)],

            _ => [],
        };
    }

    /// <summary>
    /// Extracts delivery components for an organization recipient identified by organization number.
    /// </summary>
    private static RecipientDeliveryDetails ExtractOrganizationRecipientComponents(RecipientOrganization recipientOrganization)
    {
        var (templates, smsSendingTimePolicy) = ExtractTemplatesFromSettings(recipientOrganization.SmsSettings, recipientOrganization.EmailSettings);

        return new RecipientDeliveryDetails
        {
            Templates = templates,
            SmsSendingTimePolicy = smsSendingTimePolicy,
            Channel = recipientOrganization.ChannelSchema,
            ResourceId = recipientOrganization.ResourceId,
            Recipients = [new([], organizationNumber: recipientOrganization.OrgNumber)]
        };
    }

    /// <summary>
    /// Filters recipients who are missing contact information for the specified notification channel.
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
    private static List<Recipient> FilterRecipientsWithoutContactPoints(NotificationChannel channel, List<Recipient> recipients)
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

    /// <summary>
    /// Extracts delivery components for an external identity recipient.
    /// </summary>
    private static RecipientDeliveryDetails ExtractExternalIdentityRecipientComponents(RecipientExternalIdentity recipientExternalIdentity)
    {
        var (templates, smsSendingTimePolicy) = ExtractTemplatesFromSettings(recipientExternalIdentity.SmsSettings, recipientExternalIdentity.EmailSettings);

        return new RecipientDeliveryDetails
        {
            Templates = templates,
            SmsSendingTimePolicy = smsSendingTimePolicy,
            Channel = recipientExternalIdentity.ChannelSchema,
            ResourceId = recipientExternalIdentity.ResourceId,
            Recipients = [new([], externalIdentity: recipientExternalIdentity.ExternalIdentity)]
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
    /// <returns>
    /// A <see cref="Result{T}"/> containing either:
    /// <list type="bullet">
    /// <item>
    /// <description>A successful result with <see cref="Result{T}.Value"/> containing the fully configured
    /// <see cref="NotificationOrder"/> ready for persistence and processing</description>
    /// </item>
    /// <item>
    /// <description>A failed result with <see cref="Result{T}.Problem"/> containing a
    /// <see cref="ProblemInstance"/> with error code and detailed error message about the failure reason</description>
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
    private async Task<Result<NotificationOrder>> CreateMainNotificationOrderAsync(NotificationOrderChainRequest orderRequest, DateTime currentTime)
    {
        var deliveryDetails = ExtractDeliveryDetails(orderRequest.Recipient);

        var lookupResult = await GetRecipientLookupResult(deliveryDetails.Recipients, deliveryDetails.Channel, deliveryDetails.ResourceId);

        if (lookupResult?.MissingContact?.Count > 0)
        {
            return Problems.MissingContactInformation;
        }

        var templates = SetSenderIfNotDefined(deliveryDetails.Templates);

        return new NotificationOrder
        {
            Created = currentTime,
            Templates = templates,
            Type = orderRequest.Type,
            Id = orderRequest.OrderId,
            Creator = orderRequest.Creator,
            ResourceId = deliveryDetails.ResourceId,
            Recipients = deliveryDetails.Recipients,
            NotificationChannel = deliveryDetails.Channel,
            SendersReference = orderRequest.SendersReference,
            RequestedSendTime = orderRequest.RequestedSendTime,
            ConditionEndpoint = orderRequest.ConditionEndpoint,
            IgnoreReservation = deliveryDetails.IgnoreReservation,
            SendingTimePolicy = deliveryDetails.SmsSendingTimePolicy
        };
    }

    /// <summary>
    /// Performs contact point lookup for recipients missing contact information and builds a lookup result.
    /// </summary>
    /// <param name="originalRecipients">
    /// The list of <see cref="Recipient"/> objects to evaluate for missing contact points.
    /// </param>
    /// <param name="channel">
    /// The <see cref="NotificationChannel"/> that determines which type of contact information is required.
    /// </param>
    /// <param name="resourceId">
    /// An optional resource identifier used for authorization during contact point lookup.
    /// </param>
    /// <returns>
    /// A <see cref="RecipientLookupResult"/> containing information about reserved recipients and those
    /// with missing contact details, or <c>null</c> if all recipients already have the required contact information.
    /// </returns>
    private async Task<RecipientLookupResult?> GetRecipientLookupResult(List<Recipient> originalRecipients, NotificationChannel channel, string? resourceId)
    {
        List<Recipient> recipientsWithoutContactPoint = FilterRecipientsWithoutContactPoints(channel, originalRecipients);
        if (recipientsWithoutContactPoint.Count == 0)
        {
            return null;
        }

        switch (channel)
        {
            case NotificationChannel.Email:
                await _contactPointService.AddEmailContactPoints(recipientsWithoutContactPoint, resourceId, OrderPhase.Processing);
                break;

            case NotificationChannel.Sms:
                await _contactPointService.AddSmsContactPoints(recipientsWithoutContactPoint, resourceId, OrderPhase.Processing);
                break;

            case NotificationChannel.EmailAndSms:
                await _contactPointService.AddEmailAndSmsContactPointsAsync(recipientsWithoutContactPoint, resourceId, OrderPhase.Processing);
                break;

            case NotificationChannel.SmsPreferred:
            case NotificationChannel.EmailPreferred:
                await _contactPointService.AddPreferredContactPoints(channel, recipientsWithoutContactPoint, resourceId, OrderPhase.Processing);
                break;
        }

        var isReserved = recipientsWithoutContactPoint.Where(r => r.IsReserved.HasValue && r.IsReserved.Value).Select(r => r.NationalIdentityNumber!).ToList();

        RecipientLookupResult lookupResult = new()
        {
            IsReserved = isReserved,
            MissingContact = [.. GetMissingContactListIds(channel, recipientsWithoutContactPoint).Except(isReserved)]
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
    /// Extracts notification templates from the provided SMS and email settings.
    /// </summary>
    private static (List<INotificationTemplate> Templates, SendingTimePolicy? SmsSendingTimePolicy) ExtractTemplatesFromSettings(SmsSendingOptions? smsSettings, EmailSendingOptions? emailSettings)
    {
        var templates = new List<INotificationTemplate>();
        SendingTimePolicy? smsSendingTimePolicy = null;

        if (smsSettings != null)
        {
            templates.Add(CreateSmsTemplate(smsSettings));
            smsSendingTimePolicy = smsSettings.SendingTimePolicy;
        }

        if (emailSettings != null)
        {
            templates.Add(CreateEmailTemplate(emailSettings));
        }

        return (templates, smsSendingTimePolicy);
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
    /// A <see cref="Result{T}"/> containing either:
    /// <list type="bullet">
    /// <item>
    /// <description>A successful result with <see cref="Result{T}.Value"/> containing a list of
    /// fully configured <see cref="NotificationOrder"/> objects ready for persistence</description>
    /// </item>
    /// <item>
    /// <description>A failed result with <see cref="Result{T}.Problem"/> containing a
    /// <see cref="ProblemInstance"/> with details about what went wrong during reminder processing</description>
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
    /// Processing stops at the first reminder that fails validation, returning the corresponding error.
    /// If the input list is empty or null, an empty list is returned.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    private async Task<Result<List<NotificationOrder>>> CreateReminderNotificationOrdersAsync(List<NotificationReminder>? notificationReminders, Creator creator, DateTime currentTime, CancellationToken cancellationToken)
    {
        var reminders = new List<NotificationOrder>();
        if (notificationReminders is not { Count: > 0 })
        {
            return reminders;
        }

        foreach (var notificationReminder in notificationReminders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deliveryDetails = ExtractDeliveryDetails(notificationReminder.Recipient);

            var lookupResult = await GetRecipientLookupResult(deliveryDetails.Recipients, deliveryDetails.Channel, deliveryDetails.ResourceId);

            if (lookupResult?.MissingContact?.Count > 0)
            {
                return Problems.MissingContactInformation;
            }

            var templates = SetSenderIfNotDefined(deliveryDetails.Templates);

            reminders.Add(new NotificationOrder
            {
                Creator = creator,
                Templates = templates,
                Created = currentTime,
                Type = notificationReminder.Type,
                Id = notificationReminder.OrderId,
                Recipients = deliveryDetails.Recipients,
                ResourceId = deliveryDetails.ResourceId,
                NotificationChannel = deliveryDetails.Channel,
                IgnoreReservation = deliveryDetails.IgnoreReservation,
                SendingTimePolicy = deliveryDetails.SmsSendingTimePolicy,
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
    /// A <see cref="Result{T}"/> containing a successful result with <see cref="Result{T}.Value"/> 
    /// containing a <see cref="NotificationOrderChainResponse"/> with the chain identifier and receipt information.
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the repository fails to persist the order chain.
    /// </exception>
    private async Task<Result<NotificationOrderChainResponse>> CreateChainResponseAsync(NotificationOrderChainRequest orderRequest, NotificationOrder mainOrder, List<NotificationOrder>? reminderOrders, CancellationToken cancellationToken)
    {
        var savedOrders = await _repository.Create(orderRequest, mainOrder, reminderOrders, cancellationToken);

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
}
