using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IEmailOrderProcessingService"/>
/// </summary>
public class EmailOrderProcessingService : IEmailOrderProcessingService
{
    private readonly IEmailNotificationRepository _emailNotificationRepository;
    private readonly IEmailNotificationService _emailService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public EmailOrderProcessingService(
        IEmailNotificationRepository emailNotificationRepository,
        IEmailNotificationService emailService)
    {
        _emailNotificationRepository = emailNotificationRepository;
        _emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
    {
        foreach (Recipient recipient in order.Recipients)
        {
            await _emailService.CreateNotification(order.Id, order.RequestedSendTime, recipient);
        }
    }

    /// <inheritdoc/>   
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        List<EmailRecipient> emailRecipients = await _emailNotificationRepository.GetRecipients(order.Id);
        foreach (Recipient recipient in order.Recipients)
        {
            EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

            if (!emailRecipients.Exists(er =>
                er.NationalIdentityNumber == recipient.NationalIdentityNumber
                && er.NationalIdentityNumber == recipient.OrganisationNumber
                && er.ToAddress.Equals(addressPoint?.EmailAddress)))
            {
                await _emailService.CreateNotification(order.Id, order.RequestedSendTime, recipient);
            }
        }
    }
}
