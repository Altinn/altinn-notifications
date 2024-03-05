using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
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
        foreach (Recipient recipient in order.Recipients)
        {
            await _smsService.CreateNotification(order.Id, order.RequestedSendTime, recipient);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        List<SmsRecipient> smsRecipients = await _smsNotificationRepository.GetRecipients(order.Id);
        foreach (Recipient recipient in order.Recipients)
        {
            SmsAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Sms) as SmsAddressPoint;

            if (!smsRecipients.Exists(sr =>
                sr.NationalIdentityNumber == recipient.NationalIdentityNumber
                && sr.OrganisationNumber == recipient.OrganisationNumber
                && sr.MobileNumber.Equals(addressPoint?.MobileNumber)))
            {
                await _smsService.CreateNotification(order.Id, order.RequestedSendTime, recipient);
            }
        }
    }
}
