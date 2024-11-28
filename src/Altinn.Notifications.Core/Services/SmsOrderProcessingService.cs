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
    private readonly ISmsNotificationRepository _smsNotificationRepository;
    private readonly ISmsNotificationService _smsService;
    private readonly IContactPointService _contactPointService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public SmsOrderProcessingService(ISmsNotificationRepository smsNotificationRepository, ISmsNotificationService smsService, IContactPointService contactPointService)
    {
        _smsNotificationRepository = smsNotificationRepository;
        _smsService = smsService;
        _contactPointService = contactPointService;
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
    {
        var recipients = order.Recipients;
        var recipientsWithoutMobileNumber = recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms)).ToList();
        await _contactPointService.AddSmsContactPoints(recipientsWithoutMobileNumber, order.ResourceId);

        await ProcessOrderWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        int smsCount = GetSmsCountForOrder(order);

        var smsTemplate = order.Templates[0] as SmsTemplate;

        foreach (Recipient recipient in recipients)
        {
            await _smsService.CreateNotification(order.Id, order.RequestedSendTime, recipient, smsCount, order.IgnoreReservation ?? false, smsTemplate?.Body);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        var recipients = order.Recipients;
        var recipientsWithoutMobileNumber = recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Sms)).ToList();

        await _contactPointService.AddSmsContactPoints(recipientsWithoutMobileNumber, order.ResourceId);

        await ProcessOrderRetryWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        int smsCount = GetSmsCountForOrder(order);
        List<SmsRecipient> smsRecipients = await _smsNotificationRepository.GetRecipients(order.Id);

        foreach (Recipient recipient in recipients)
        {
            SmsAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Sms) as SmsAddressPoint;

            if (!smsRecipients.Exists(sr =>
                sr.NationalIdentityNumber == recipient.NationalIdentityNumber
                && sr.OrganizationNumber == recipient.OrganizationNumber
                && sr.MobileNumber == addressPoint?.MobileNumber))
            {
                await _smsService.CreateNotification(order.Id, order.RequestedSendTime, recipient, smsCount);
            }
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

    private static int GetSmsCountForOrder(NotificationOrder order)
    {
        SmsTemplate? smsTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Sms) as SmsTemplate;
        return CalculateNumberOfMessages(smsTemplate!.Body);
    }
}
