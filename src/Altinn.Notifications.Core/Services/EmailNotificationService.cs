using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="IEmailNotificationService"/>
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly IEmailNotificationsRepository _repository;
    private readonly IGuidService _guid;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationService"/> class.
    /// </summary>
    public EmailNotificationService(IEmailNotificationsRepository repository, IGuidService guid)
    {
        _repository = repository;
        _guid = guid;
    }

    /// <inheritdoc/>
    public async Task CreateEmailNotification(string orderId, DateTime requestedSendTime, EmailTemplate emailTemplate, Recipient recipient)
    {
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        if (!string.IsNullOrEmpty(addressPoint?.EmailAddress))
        {
            await GenerateEmailNotificationForRecipient(orderId, requestedSendTime, recipient.RecipientId, addressPoint.EmailAddress);
        }
        else
        {
            /*
             * generate emailNotification with a failure result as we don't have recipient contact info. 
             *  GenerateFailedEmailNotificationForRecipient(order, recipientId, "No email address identified for recipient");
            */
        }
    }

    private async Task GenerateEmailNotificationForRecipient(string orderId, DateTime requestedSendTime, string recipientId, string toAddress)
    {
        var emailNotification = new EmailNotification()
        {
            Id = _guid.NewGuidAsString(),
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            ToAddress = toAddress,
            RecipientId = string.IsNullOrEmpty(recipientId) ? null : recipientId
        };

        await _repository.AddEmailNotification(emailNotification);
    }
}