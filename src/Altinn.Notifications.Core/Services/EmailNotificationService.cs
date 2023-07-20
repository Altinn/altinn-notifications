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
            await GenerateEmailNotificationForRecipient(orderId, requestedSendTime, recipient.RecipientId, string.Empty, EmailNotificationResultType.Failed_RecipientNotIdentified);
        }
    }

    private async Task GenerateEmailNotificationForRecipient(string orderId, DateTime requestedSendTime, string recipientId, string toAddress, EmailNotificationResultType result = EmailNotificationResultType.New)
    {
        var emailNotification = new EmailNotification()
        {
            Id = _guid.NewGuidAsString(),
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            ToAddress = toAddress,
            RecipientId = string.IsNullOrEmpty(recipientId) ? null : recipientId,
            SendResult = new(result)
        };

        DateTime expiry;

        if ( result == EmailNotificationResultType.Failed_RecipientNotIdentified )
        {
            expiry = DateTime.UtcNow;
        }
        else {
            // lets see how much time it takes to get a status for communication services
            expiry = requestedSendTime.AddHours(1);
        }

        await _repository.AddEmailNotification(emailNotification, expiry);
    }
}