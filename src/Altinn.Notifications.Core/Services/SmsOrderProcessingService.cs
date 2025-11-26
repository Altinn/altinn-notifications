using System.Web;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
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
    public async Task ProcessOrder(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        await ProcessOrderWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        await ProcessOrderRetryWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var smsTemplate = GetValidatedSmsTemplate(order);

        var expirationDateTime = GetExpirationDateTime(order);

        var segmentsCount = CalculateSegmentCount(smsTemplate.Body);

        var allSmsRecipients = await GetSmsRecipientsAsync(recipients, smsTemplate.Body);

        var registeredSmsRecipients = await _smsNotificationRepository.GetRecipients(order.Id);
        
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

            await _smsService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                expirationDateTime,
                smsAddresses,
                smsRecipient,
                segmentsCount);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var smsTemplate = GetValidatedSmsTemplate(order);

        var expirationDateTime = GetExpirationDateTime(order);

        var segmentsCount = CalculateSegmentCount(smsTemplate.Body);

        var allSmsRecipients = await GetSmsRecipientsAsync(recipients, smsTemplate.Body);

        foreach (var recipient in recipients)
        {
            var smsAddresses = recipient.AddressInfo
                .OfType<SmsAddressPoint>()
                .Where(a => !string.IsNullOrWhiteSpace(a.MobileNumber))
                .ToList();

            var smsRecipient = FindSmsRecipient(allSmsRecipients, recipient) ?? new SmsRecipient { IsReserved = recipient.IsReserved };

            await _smsService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                expirationDateTime,
                smsAddresses,
                smsRecipient,
                segmentsCount,
                order.IgnoreReservation ?? false);
        }
    }

    /// <summary>
    /// Calculates the number of messages based on the rules for concatenation of SMS messages in the SMS gateway.
    /// </summary>
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

        // Calculate the number of messages for messages exceeding 160 characters
        int numberOfMessages = (int)Math.Ceiling((double)messageLength / charactersPerConcatenatedMessage);

        // Check if the total number of messages exceeds the limit
        if (numberOfMessages > maxMessagesPerConcatenation)
        {
            numberOfMessages = maxMessagesPerConcatenation;
        }

        return numberOfMessages;
    }

    /// <summary>
    /// Determines whether the specified template part requires customization by checking for placeholder keywords.
    /// </summary>
    /// <param name="templatePart">The part of the SMS template (The SMS body) to evaluate.</param>
    /// <returns><c>true</c> if the template part contains placeholders for recipient-specific customization; otherwise, <c>false</c>.</returns>
    private bool RequiresCustomization(string? templatePart)
    {
        return _keywordsService.ContainsRecipientNumberPlaceholder(templatePart) || _keywordsService.ContainsRecipientNamePlaceholder(templatePart);
    }

    /// <summary>
    /// Calculates the expiration date and time for an SMS notification based on the order's sending time policy.
    /// </summary>
    /// <param name="order">The notification order containing the requested send time and sending time policy.</param>
    /// <returns>
    /// The expiration <see cref="DateTime"/> for the SMS notification. 
    /// If the sending time policy is <see cref="SendingTimePolicy.Daytime"/>, the expiration is determined by the notification schedule service.
    /// Otherwise, it defaults to 48 hours after the requested send time.
    /// </returns>
    private DateTime GetExpirationDateTime(NotificationOrder order)
    {
        return order.SendingTimePolicy switch
        {
            SendingTimePolicy.Daytime => _notificationScheduleService.GetSmsExpirationDateTime(order.RequestedSendTime),
            _ => order.RequestedSendTime.AddHours(48),
        };
    }

    /// <summary>
    /// Retrieves and validates the SMS template from the notification order.
    /// </summary>
    /// <param name="order">The notification order containing the list of templates.</param>
    /// <returns>
    /// The <see cref="SmsTemplate"/> found in the order's templates.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if an SMS template is not found or is not of the correct type.
    /// </exception>
    private static SmsTemplate GetValidatedSmsTemplate(NotificationOrder order)
    {
        if (order.Templates.Find(e => e.Type == NotificationTemplateType.Sms) is not SmsTemplate smsTemplate)
        {
            throw new InvalidOperationException("SMS template is not found or is not of the correct type.");
        }

        return smsTemplate;
    }

    /// <summary>
    /// Ensures all recipients in the notification order have SMS contact points.
    /// Looks up and adds SMS contact points for recipients missing them.
    /// </summary>
    /// <param name="order">The notification order containing the recipients.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The result is the updated list of recipients.
    /// </returns>
    private async Task<List<Recipient>> UpdateRecipientsWithContactPointsAsync(NotificationOrder order)
    {
        var recipientsMissingSmsContact = order.Recipients
            .Where(e => e.AddressInfo.All(e => e.AddressType != AddressType.Sms))
            .ToList();

        if (recipientsMissingSmsContact.Count > 0)
        {
            await _contactPointService.AddSmsContactPoints(recipientsMissingSmsContact, order.ResourceId);
        }

        return order.Recipients;
    }

    /// <summary>
    /// Finds the SMS recipient matching the given recipient.
    /// </summary>
    /// <param name="smsRecipients">The list of SMS recipients.</param>
    /// <param name="recipient">The recipient to match.</param>
    /// <returns>The matching SMS recipient, or null if no match is found.</returns>
    private static SmsRecipient? FindSmsRecipient(IEnumerable<SmsRecipient> smsRecipients, Recipient recipient)
    {
        return smsRecipients.FirstOrDefault(er =>
            (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber) && er.OrganizationNumber == recipient.OrganizationNumber) ||
            (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber) && er.NationalIdentityNumber == recipient.NationalIdentityNumber));
    }

    /// <summary>
    /// Creates a collection of <see cref="SmsRecipient"/> for SMS delivery.
    /// </summary>
    /// <param name="recipients">The recipients to process.</param>
    /// <param name="messageBody">The SMS message body.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The result contains the collection of <see cref="SmsRecipient"/> with customized message bodies.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recipients"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="messageBody"/> is null or whitespace.</exception>
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
