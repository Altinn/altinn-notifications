using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public SmsOrderProcessingService(ISmsNotificationRepository smsNotificationRepository, ISmsNotificationService smsService)
    {
        _smsNotificationRepository = smsNotificationRepository;
        _smsService = smsService;
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
    {
        int smsCount = GetSmsCountForOrder(order);

        foreach (Recipient recipient in order.Recipients)
        {
            await _smsService.CreateNotification(order.Id, order.RequestedSendTime, recipient, smsCount);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        int smsCount = GetSmsCountForOrder(order);
        List<SmsRecipient> smsRecipients = await _smsNotificationRepository.GetRecipients(order.Id);
        foreach (Recipient recipient in order.Recipients)
        {
            SmsAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Sms) as SmsAddressPoint;

            if (!smsRecipients.Exists(sr =>
                sr.RecipientId == (string.IsNullOrEmpty(recipient.RecipientId) ? null : recipient.RecipientId)
                && sr.MobileNumber.Equals(addressPoint?.MobileNumber)))
            {
                await _smsService.CreateNotification(order.Id, order.RequestedSendTime, recipient, smsCount);
            }
        }
    }

    private static int GetSmsCountForOrder(NotificationOrder order)
    {
        SmsTemplate? smsTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Sms) as SmsTemplate;
        return CalculateNumberOfMessages(smsTemplate!.Body);
    }

    private static int CalculateNumberOfMessages(string message)
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
}
