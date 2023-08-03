using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.IntegrationTests.Utils;
public static class TestdataUtil
{
    public static (NotificationOrder order, EmailNotification notification) GetOrderAndEmailNotification()
    {
        NotificationOrder order = NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var recipient = order.Recipients.First();
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        var emailNotification = new EmailNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = order.RequestedSendTime,
            ToAddress = addressPoint!.EmailAddress,
            RecipientId = recipient.RecipientId,
            SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow)
        };

        return (order, emailNotification);
    }

    /// <summary>
    /// NOTE! Overwrite id with a new GUID to ensure it is unique in the test scope.
    /// </summary>
    public static NotificationOrder NotificationOrder_EmailTemplate_OneRecipient()
    {
        return new NotificationOrder()
        {
            SendersReference = "local-testing",
            Templates = new List<INotificationTemplate>()
            {
                new EmailTemplate()
                {
                    Type = NotificationTemplateType.Email,
                    FromAddress = "sender@domain.com",
                    Subject = "email-subject",
                    Body = "email-body",
                    ContentType = EmailContentType.Html
                }
            },
            RequestedSendTime = DateTime.UtcNow,
            NotificationChannel = NotificationChannel.Email,
            Creator = new("ttd"),
            Created = DateTime.UtcNow,
            Recipients = new List<Recipient>()
            {
                new Recipient()
                {
                    RecipientId = "recipient1",
                    AddressInfo = new()
                    {
                        new EmailAddressPoint()
                        {
                            AddressType = AddressType.Email,
                            EmailAddress = "recipient1@domain.com"
                        }
                    }
                }
            }
        };
    }
}
