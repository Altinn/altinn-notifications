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
    public async Task<Result<NotificationOrderChainResponse, ServiceError>> RegisterNotificationOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default)
    {
        DateTime currentTime = _dateTime.UtcNow();

        cancellationToken.ThrowIfCancellationRequested();

        // Create the main order from the request.
        var mainOrder = new NotificationOrder();
        Result<NotificationOrder, ServiceError> mainOrderCreationResult = await CreateNotificationOrder(orderRequest, currentTime);
        if (mainOrderCreationResult.IsSuccess && mainOrderCreationResult.Value != null)
        {
            mainOrder = mainOrderCreationResult.Value;
        }
        else if (mainOrderCreationResult.IsError && mainOrderCreationResult.Error != null)
        {
            return mainOrderCreationResult.Error;
        }

        // Create the reminders from the request.
        var reminderOrders = new List<NotificationOrder>();
        if (orderRequest.Reminders is { Count: > 0 })
        {
            foreach (var reminder in orderRequest.Reminders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Result<NotificationOrder, ServiceError> result = await CreateNotificationReminder(reminder, orderRequest.Creator, currentTime);

                if (result.IsSuccess && result.Value != null)
                {
                    reminderOrders.Add(result.Value);
                }
                else if (result.IsError && result.Error != null)
                {
                    return result.Error;
                }
            }
        }

        List<NotificationOrder> savedOrders = await _repository.Create(orderRequest, mainOrder, reminderOrders, cancellationToken);
        if (savedOrders == null || savedOrders.Count == 0)
        {
            return new ServiceError(422, "Failed to create the notification order chain.");
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
                    ? [.. savedOrders
                        .Where(e => e.Id != savedMainOrder.Id)
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
    /// Creates a notification order from a chain request with validated recipient information.
    /// </summary>
    /// <param name="orderRequest">
    /// The request containing all necessary notification delivery details, including recipient information,
    /// order identifiers, scheduling parameters, and content references.
    /// </param>
    /// <param name="currentTime">
    /// The timestamp to use as the creation time for this notification order.
    /// </param>
    /// <returns>
    /// A <see cref="Result{T, TError}"/> containing either:
    /// <list type="bullet">
    /// <item><description>A successfully created <see cref="NotificationOrder"/> with all necessary configuration</description></item>
    /// <item><description>A <see cref="ServiceError"/> if recipient contact information cannot be resolved</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method performs these key operations:
    /// <list type="number">
    /// <item><description>Extracts delivery details from the recipient configuration</description></item>
    /// <item><description>Validates recipient contact information through lookup services</description></item>
    /// <item><description>Applies default sender information to message templates where needed</description></item>
    /// <item><description>Constructs a complete notification order ready for processing</description></item>
    /// </list>
    /// </remarks>
    private async Task<Result<NotificationOrder, ServiceError>> CreateNotificationOrder(NotificationOrderChainRequest orderRequest, DateTime currentTime)
    {
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
    /// Creates a notification reminder based on the provided reminder details.
    /// </summary>
    /// <param name="reminderRequest">
    /// The reminder containing all notification delivery specifications,
    /// including recipient information, reminder identifiers, and content templates.
    /// </param>
    /// <param name="creator">
    /// The creator information that identifies who initiated the notification.
    /// </param>
    /// <param name="currentTime">
    /// The timestamp to use as the creation time for this notification reminder.
    /// </param>
    /// <returns>
    /// A <see cref="Result{T, TError}"/> containing either:
    /// <list type="bullet">
    /// <item><description>A successfully created <see cref="NotificationOrder"/> configured as a reminder</description></item>
    /// <item><description>A <see cref="ServiceError"/> if recipient contact information cannot be resolved</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method performs these key operations:
    /// <list type="number">
    /// <item><description>Extracts delivery components from the recipient configuration</description></item>
    /// <item><description>Validates recipient contact information through lookup services</description></item>
    /// <item><description>Applies default sender information to message templates where needed</description></item>
    /// <item><description>Constructs a notification order configured as a reminder</description></item>
    /// </list>
    /// </remarks>
    private async Task<Result<NotificationOrder, ServiceError>> CreateNotificationReminder(NotificationReminder reminderRequest, Creator creator, DateTime currentTime)
    {
        var (recipients, templates, channel, ignoreReservation, resourceId, sendingTimePolicyForSms) = ExtractDeliveryComponents(reminderRequest.Recipient);

        var lookupResult = await GetRecipientLookupResult(recipients, channel, GetSanitizedResourceId(resourceId));

        if (lookupResult?.MissingContact?.Count > 0)
        {
            return new ServiceError(422, $"Missing contact information for recipient(s): {string.Join(", ", lookupResult.MissingContact)}");
        }

        templates = SetSenderIfNotDefined(templates);

        return new NotificationOrder
        {
            Creator = creator,
            Templates = templates,
            Created = currentTime,
            Recipients = recipients,
            ResourceId = resourceId,
            Type = reminderRequest.Type,
            Id = reminderRequest.OrderId,
            NotificationChannel = channel,
            IgnoreReservation = ignoreReservation,
            SendingTimePolicy = sendingTimePolicyForSms,
            SendersReference = reminderRequest.SendersReference,
            RequestedSendTime = reminderRequest.RequestedSendTime,
            ConditionEndpoint = reminderRequest.ConditionEndpoint
        };
    }

    private static string? GetSanitizedResourceId(string? resourceId)
    {
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            // Only perform replace if the prefix exists
            if (resourceId.StartsWith("urn:altinn:resource:"))
            {
                return resourceId.Replace("urn:altinn:resource:", string.Empty);
            }

            return resourceId;
        }

        return null;
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
