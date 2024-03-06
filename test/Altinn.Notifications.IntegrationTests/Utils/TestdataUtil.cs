using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class TestdataUtil
{
    public static (NotificationOrder Order, SmsNotification Notification) GetOrderAndSmsNotification()
    {
        NotificationOrder order = NotificationOrder_SmsTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var recipient = order.Recipients[0];
        SmsAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Sms) as SmsAddressPoint;

        var smsNotification = new SmsNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = order.RequestedSendTime,
            Recipient = new()
            {
                MobileNumber = addressPoint!.MobileNumber,
            },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        return (order, smsNotification);
    }

    public static (NotificationOrder Order, EmailNotification Notification) GetOrderAndEmailNotification()
    {
        NotificationOrder order = NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        var recipient = order.Recipients[0];
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        var emailNotification = new EmailNotification()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = order.RequestedSendTime,
            Recipient = new()
            {
                ToAddress = addressPoint!.EmailAddress
            },
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
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc),
            NotificationChannel = NotificationChannel.Email,
            Creator = new("ttd"),
            Created = new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc),
            Recipients = new List<Recipient>()
            {
                new Recipient()
                {
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

    /// <summary>
    /// NOTE! Overwrite id with a new GUID to ensure it is unique in the test scope.
    /// </summary>
    public static NotificationOrder NotificationOrder_SmsTemplate_OneRecipient()
    {
        return new NotificationOrder()
        {
            SendersReference = "local-testing",
            Templates = new List<INotificationTemplate>()
            {
                new SmsTemplate()
                {
                    Type = NotificationTemplateType.Sms,
                    Body = "sms-body",
                    SenderNumber = "Altinn local test"
                }
            },
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc),
            NotificationChannel = NotificationChannel.Sms,
            Creator = new("ttd"),
            Created = new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc),
            Recipients = new List<Recipient>()
            {
                new Recipient()
                {
                    AddressInfo = new()
                    {
                        new SmsAddressPoint()
                        {
                            AddressType = AddressType.Sms,
                            MobileNumber = "+4799999999"
                        }
                    }
                }
            }
        };
    }
}
