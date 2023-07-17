using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="IEmailNotificationService"/>
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly GuidService _guid;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationService"/> class.
    /// </summary>
    public EmailNotificationService(GuidService guid)
    {
        _guid = guid;
    }

    /// <inheritdoc/>
    public void ProcessEmailNotification(string orderId, EmailTemplate emailTemplate, Recipient recipient)
    {
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        if (!string.IsNullOrEmpty(addressPoint?.EmailAddress))
        {
            GenerateEmailNotificationForRecipient(orderId, recipient.RecipientId, addressPoint.EmailAddress);
        }
        else
        {
            // generate emailNotification with a failure status as we don't have contenct. 
            // GenerateFailedEmailNotificationForRecipient(order, recipientId, "No email address identified for recipient");
        }

        // save in DB. With default status ? 
    }

    private EmailNotification GenerateEmailNotificationForRecipient(string orderId, string recipientId, string toAddress)
    {
        var emailNotification = new EmailNotification()
        {
            Id = _guid.NewGuidAsString(),
            OrderId = orderId,
            ToAddress = toAddress,
            RecipientId = string.IsNullOrEmpty(recipientId) ? null : recipientId
        };

        return emailNotification;
    }
}