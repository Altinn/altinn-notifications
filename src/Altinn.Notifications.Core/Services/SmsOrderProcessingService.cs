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
        if (order.Templates.Find(t => t.Type == NotificationTemplateType.Sms) is not SmsTemplate smsTemplate)
        {
            throw new InvalidOperationException("SMS template is not found or is not of the correct type.");
        }

        var expiryDateTime = GetExpiryDateTime(order);

        var messagesCount = CalculateNumberOfMessages(smsTemplate.Body);

        var allSmsRecipients = await GetSmsRecipientsAsync(recipients, smsTemplate.Body);

        var registeredSmsRecipients = await _smsNotificationRepository.GetRecipients(order.Id);

        foreach (var recipient in recipients)
        {
            var smsAddress = recipient.AddressInfo.OfType<SmsAddressPoint>().FirstOrDefault();
            if (smsAddress == null)
            {
                continue;
            }

            var isSmsRecipientRegistered =
                registeredSmsRecipients.Exists(er =>
                                               er.MobileNumber == smsAddress.MobileNumber &&
                                               er.OrganizationNumber == recipient.OrganizationNumber &&
                                               er.NationalIdentityNumber == recipient.NationalIdentityNumber);
            if (isSmsRecipientRegistered)
            {
                continue;
            }

            var matchedSmsRecipient = FindSmsRecipient(allSmsRecipients, recipient);
            var smsRecipient = matchedSmsRecipient ?? new SmsRecipient { IsReserved = recipient.IsReserved };

            await _smsService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                expiryDateTime,
                [smsAddress],
                smsRecipient,
                messagesCount);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        if (order.Templates.Find(e => e.Type == NotificationTemplateType.Sms) is not SmsTemplate smsTemplate)
        {
            throw new InvalidOperationException("SMS template is not found or is not of the correct type.");
        }

        var expiryDateTime = GetExpiryDateTime(order);

        var messagesCount = CalculateNumberOfMessages(smsTemplate.Body);

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
                expiryDateTime,
                smsAddresses,
                smsRecipient,
                messagesCount,
                order.IgnoreReservation ?? false);
        }
    }

    /// <summary>
    /// Calculates the number of messages based on the rules for concatenation of SMS messages in the SMS gateway.
    /// </summary>
    internal static int CalculateNumberOfMessages(string message)
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
    /// Calculates the expiry date and time for an SMS notification based on the sending time policy and current SMS send window.
    /// </summary>
    /// <remarks>
    /// - If the sending time policy is <see cref="SendingTimePolicy.Anytime"/>, the expiry time is set to 2 days after the requested send time.
    /// - If the sending time policy is <see cref="SendingTimePolicy.Daytime"/> and the current time is within the allowed window, expiry time is also set to 2 days after the requested send time.
    /// - Otherwise, the expiry time is set to 3 days after the requested send time.
    /// </remarks>
    /// <param name="order">The notification order containing the requested send time and sending time policy.</param>
    /// <returns>The calculated expiry time for the notification order.</returns>
    private DateTime GetExpiryDateTime(NotificationOrder order)
    {
        return order.SendingTimePolicy switch
        {
            SendingTimePolicy.Anytime => order.RequestedSendTime.AddDays(2),

            _ => _notificationScheduleService.CanSendSmsNotifications() ? order.RequestedSendTime.AddDays(2) : order.RequestedSendTime.AddDays(3),
        };
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
            .Where(r => r.AddressInfo.All(a => a.AddressType != AddressType.Sms))
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
        });

        return await _keywordsService.ReplaceKeywordsAsync(smsRecipients);
    }
}
