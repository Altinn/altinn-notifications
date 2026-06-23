using System.Web;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="ISmsOrderProcessingService"/>
/// </summary>
public class SmsOrderProcessingService : ISmsOrderProcessingService
{
    private readonly IKeywordsService _keywordsService;
    private readonly ISmsNotificationService _smsService;
    private readonly IContactPointService _contactPointService;
    private readonly ISmsNotificationRepository _smsNotificationRepository;
    private readonly INotificationScheduleService _notificationScheduleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOrderProcessingService"/> class.
    /// </summary>
    public SmsOrderProcessingService(
        IKeywordsService keywordsService,
        ISmsNotificationService smsService,
        IContactPointService contactPointService,
        ISmsNotificationRepository smsNotificationRepository,
        INotificationScheduleService notificationScheduleService)
    {
        _smsService = smsService;
        _keywordsService = keywordsService;
        _contactPointService = contactPointService;
        _smsNotificationRepository = smsNotificationRepository;
        _notificationScheduleService = notificationScheduleService;
    }

    /// <inheritdoc/>
    public async Task<SmsOrderProcessingResult> ProcessOrder(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        return await ProcessOrderWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task<SmsOrderProcessingResult> ProcessOrderRetry(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        return await ProcessOrderRetryWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task<SmsOrderProcessingResult> ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var smsTemplate = GetValidatedSmsTemplate(order);
        var expirationDateTime = GetExpirationDateTime(order);
        var segmentsCount = CalculateSegmentCount(smsTemplate.Body);
        var allSmsRecipients = await GetSmsRecipientsAsync(recipients, smsTemplate.Body);
        var registeredSmsRecipients = await _smsNotificationRepository.GetRecipients(order.Id);

        var notifications = new List<SmsNotification>();

        foreach (var recipient in recipients)
        {
            var smsAddress = recipient.AddressInfo.OfType<SmsAddressPoint>().FirstOrDefault();

            var isSmsRecipientRegistered =
                registeredSmsRecipients.Exists(er =>
                    er.MobileNumber == smsAddress?.MobileNumber &&
                    er.OrganizationNumber == recipient.OrganizationNumber &&
                    er.NationalIdentityNumber == recipient.NationalIdentityNumber);

            if (isSmsRecipientRegistered)
            {
                continue;
            }

            var matchedSmsRecipient = FindSmsRecipient(allSmsRecipients, recipient);
            var smsRecipient = matchedSmsRecipient ?? new SmsRecipient { IsReserved = recipient.IsReserved };
            var smsAddresses = smsAddress != null ? [smsAddress] : new List<SmsAddressPoint>();

            var created = await _smsService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                expirationDateTime,
                smsAddresses,
                smsRecipient,
                segmentsCount,
                order.IgnoreReservation ?? false);

            notifications.AddRange(created);
        }

        return new SmsOrderProcessingResult(notifications);
    }

    /// <inheritdoc/>
    public async Task<SmsOrderProcessingResult> ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var smsTemplate = GetValidatedSmsTemplate(order);
        var expirationDateTime = GetExpirationDateTime(order);
        var segmentsCount = CalculateSegmentCount(smsTemplate.Body);
        var allSmsRecipients = await GetSmsRecipientsAsync(recipients, smsTemplate.Body);

        var notifications = new List<SmsNotification>();

        foreach (var recipient in recipients)
        {
            var smsAddresses = recipient.AddressInfo
                .OfType<SmsAddressPoint>()
                .Where(a => !string.IsNullOrWhiteSpace(a.MobileNumber))
                .ToList();

            var smsRecipient = FindSmsRecipient(allSmsRecipients, recipient) ?? new SmsRecipient { IsReserved = recipient.IsReserved };

            var created = await _smsService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                expirationDateTime,
                smsAddresses,
                smsRecipient,
                segmentsCount,
                order.IgnoreReservation ?? false);

            notifications.AddRange(created);
        }

        return new SmsOrderProcessingResult(notifications);
    }

    private static int CalculateSegmentCount(string message)
    {
        const int maxCharactersPerMessage = 160;
        const int maxMessagesPerConcatenation = 16;
        const int charactersPerConcatenatedMessage = 134;

        string urlEncodedMessage = HttpUtility.UrlEncode(message);
        int messageLength = urlEncodedMessage.Length;

        if (messageLength <= maxCharactersPerMessage)
        {
            return 1;
        }

        int numberOfMessages = (int)Math.Ceiling((double)messageLength / charactersPerConcatenatedMessage);

        if (numberOfMessages > maxMessagesPerConcatenation)
        {
            numberOfMessages = maxMessagesPerConcatenation;
        }

        return numberOfMessages;
    }

    private bool RequiresCustomization(string? templatePart)
    {
        return _keywordsService.ContainsRecipientNumberPlaceholder(templatePart) || _keywordsService.ContainsRecipientNamePlaceholder(templatePart);
    }

    private DateTime GetExpirationDateTime(NotificationOrder order)
    {
        return order.SendingTimePolicy switch
        {
            SendingTimePolicy.Daytime => _notificationScheduleService.GetSmsExpirationDateTime(order.RequestedSendTime),
            _ => order.RequestedSendTime.AddHours(48),
        };
    }

    private static SmsTemplate GetValidatedSmsTemplate(NotificationOrder order)
    {
        if (order.Templates.Find(e => e.Type == NotificationTemplateType.Sms) is not SmsTemplate smsTemplate)
        {
            throw new InvalidOperationException("SMS template is not found or is not of the correct type.");
        }

        return smsTemplate;
    }

    private async Task<List<Recipient>> UpdateRecipientsWithContactPointsAsync(NotificationOrder order)
    {
        var recipientsMissingSmsContact = order.Recipients
            .Where(e => e.AddressInfo.All(e => e.AddressType != AddressType.Sms))
            .ToList();

        if (recipientsMissingSmsContact.Count > 0)
        {
            await _contactPointService.AddSmsContactPoints(recipientsMissingSmsContact, order.ResourceId, OrderLifecycleStage.Processing, order.UseStaleContactInformation, order.ResourceAction);
        }

        return order.Recipients;
    }

    private static SmsRecipient? FindSmsRecipient(IEnumerable<SmsRecipient> smsRecipients, Recipient recipient)
    {
        return smsRecipients.FirstOrDefault(er =>
            (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber) && er.OrganizationNumber == recipient.OrganizationNumber) ||
            (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber) && er.NationalIdentityNumber == recipient.NationalIdentityNumber));
    }

    private async Task<IEnumerable<SmsRecipient>> GetSmsRecipientsAsync(IEnumerable<Recipient> recipients, string messageBody)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageBody);

        var smsRecipients = recipients.Select(recipient => new SmsRecipient
        {
            IsReserved = recipient.IsReserved,
            OrganizationNumber = recipient.OrganizationNumber,
            NationalIdentityNumber = recipient.NationalIdentityNumber,
            CustomizedBody = RequiresCustomization(messageBody) ? messageBody : null
        }).ToList();

        return await _keywordsService.ReplaceKeywordsAsync(smsRecipients);
    }
}
