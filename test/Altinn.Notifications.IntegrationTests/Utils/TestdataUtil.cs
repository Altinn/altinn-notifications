using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

using static Altinn.Notifications.Core.Models.Orders.NotificationOrder;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class TestdataUtil
{
    public static (NotificationOrder Order, SmsNotification Notification) GetOrderAndSmsNotification(string? sendersReference)
    {
        NotificationOrder order = NotificationOrder_SmsTemplate_OneRecipient(sendersReference);
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

    public static (NotificationOrder Order, EmailNotification Notification) GetOrderAndEmailNotification(string? sendersReference)
    {
        NotificationOrder order = NotificationOrder_EmailTemplate_OneRecipient(sendersReference);
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
    public static NotificationOrder NotificationOrder_EmailTemplate_OneRecipient(string? sendersReference)
    {
        return NotificationOrder
         .GetBuilder()
         .SetId(Guid.NewGuid())
         .SetSendersReference(sendersReference)
         .SetTemplates([

            new EmailTemplate()
            {
                Type = NotificationTemplateType.Email,
                FromAddress = "sender@domain.com",
                Subject = "email-subject",
                Body = "email-body",
                ContentType = EmailContentType.Html
            }
         ])
         .SetRequestedSendTime(new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc))
         .SetNotificationChannel(NotificationChannel.Email)
         .SetCreator(new Creator("ttd"))
         .SetCreated(new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc))
         .SetIgnoreReservation(false)
         .SetRecipients([

            new Recipient()
            {
                AddressInfo = [

                    new EmailAddressPoint()
                    {
                        AddressType = AddressType.Email,
                        EmailAddress = "recipient1@domain.com"
                    }
                ]
            }
         ])
         .Build();
    }

    /// <summary>
    /// NOTE! Overwrite id with a new GUID to ensure it is unique in the test scope.
    /// </summary>
    public static NotificationOrder NotificationOrder_SmsTemplate_OneRecipient(string? sendersReference)
    {
        return NotificationOrder
        .GetBuilder()
        .SetId(Guid.NewGuid())
        .SetSendersReference(sendersReference)
        .SetTemplates(
        [
            new SmsTemplate()
            {
                Type = NotificationTemplateType.Sms,
                Body = "sms-body",
                SenderNumber = "Altinn local test"
            }
        ])
        .SetRequestedSendTime(new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc))
        .SetNotificationChannel(NotificationChannel.Sms)
        .SetCreator(new Creator("ttd"))
        .SetCreated(new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc))
        .SetIgnoreReservation(false)
        .SetRecipients(
        [
            new Recipient()
            {
                AddressInfo = [

                    new SmsAddressPoint()
                    {
                        AddressType = AddressType.Sms,
                        MobileNumber = "+4799999999"
                    }
                ]
            }
        ])
        .Build();
    }

    /// <summary>
    /// Generates a notification order using the default value for each missing property. 
    /// </summary>
    public static NotificationOrder GetOrderForTest(NotificationOrderBuilder builder)
    {
        if (!builder.IdSet)
        {
            builder.SetId(Guid.NewGuid());
        }

        if (!builder.RequestedSendTimeSet)
        {
            builder.SetRequestedSendTime(DateTime.UtcNow);
        }

        if (!builder.NotificationChannelSet)
        {
            builder.SetNotificationChannel(NotificationChannel.Email);
        }

        if (!builder.CreatorSet)
        {
            builder.SetCreator(new Creator("ttd"));
        }

        if (!builder.CreatedSet)
        {
            builder.SetCreated(DateTime.UtcNow);
        }

        if (!builder.TemplatesSet)
        {
            builder.SetTemplates(new List<INotificationTemplate>());
        }

        if (!builder.RecipientsSet)
        {
            builder.SetRecipients(new List<Recipient>());
        }

        return builder.Build();
    }

    /// <summary>
    /// Generates a notification order using the default value for all properties 
    /// </summary>
    public static NotificationOrder GetOrderForTest()
    {
        return GetOrderForTest(GetBuilder());
    }
}
