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
    private readonly IContactPointService _contactPointService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
    /// </summary>
    public EmailOrderProcessingService(
        IEmailNotificationRepository emailNotificationRepository,
        IEmailNotificationService emailService,
        IContactPointService contactPointService)
    {
        _emailNotificationRepository = emailNotificationRepository;
        _emailService = emailService;
        _contactPointService = contactPointService;
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
    {
        var recipients = order.Recipients;
        var recipientsWithoutEmail = recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email)).ToList();

        List<Recipient> agumentedRecipients = await _contactPointService.GetEmailContactPoints(recipientsWithoutEmail);

        var augmentedRecipientDictionary = agumentedRecipients.ToDictionary(ar => $"{ar.NationalIdentityNumber}-{ar.OrganisationNumber}");

        foreach (Recipient originalRecipient in recipients)
        {
            if (augmentedRecipientDictionary.TryGetValue($"{originalRecipient.NationalIdentityNumber}-{originalRecipient.OrganisationNumber}", out Recipient? augmentedRecipient))
            {
                originalRecipient.AddressInfo.AddRange(augmentedRecipient!.AddressInfo);
            }

            await _emailService.CreateNotification(order.Id, order.RequestedSendTime, originalRecipient, order.IgnoreReservation);
        }
    }

    /// <inheritdoc/>   
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        var recipients = order.Recipients;
        var recipientsWithoutEmail = recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email)).ToList();

        List<Recipient> agumentedRecipients = await _contactPointService.GetEmailContactPoints(recipientsWithoutEmail);
        var augmentedRecipientDictionary = agumentedRecipients.ToDictionary(ar => $"{ar.NationalIdentityNumber}-{ar.OrganisationNumber}");

        List<EmailRecipient> emailRecipients = await _emailNotificationRepository.GetRecipients(order.Id);

        foreach (Recipient recipient in order.Recipients)
        {
            if (augmentedRecipientDictionary.TryGetValue($"{recipient.NationalIdentityNumber}-{recipient.OrganisationNumber}", out Recipient? augmentedRecipient))
            {
                recipient.AddressInfo.AddRange(augmentedRecipient!.AddressInfo);
            }

            EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

            if (!emailRecipients.Exists(er =>
             er.NationalIdentityNumber == recipient.NationalIdentityNumber
             && er.OrganisationNumber == recipient.OrganisationNumber
             && er.ToAddress == addressPoint?.EmailAddress))
            {
                await _emailService.CreateNotification(order.Id, order.RequestedSendTime, recipient, order.IgnoreReservation);
            }
        }
    }
}
