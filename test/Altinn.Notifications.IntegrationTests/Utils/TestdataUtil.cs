using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class TestdataUtil
{
    public static (NotificationOrder Order, SmsNotification Notification) GetOrderAndSmsNotification(SendingTimePolicy? sendingTimePolicy = null)
    {
        NotificationOrder order = NotificationOrder_SmsTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        if (sendingTimePolicy != null)
        {
            order.SendingTimePolicy = sendingTimePolicy;
        }

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
            },
            Type = OrderType.Notification,
            SendingTimePolicy = SendingTimePolicy.Anytime
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
            },
            Type = OrderType.Notification,
            SendingTimePolicy = SendingTimePolicy.Daytime
        };
    }

    /// <summary>
    /// NOTE! Overwrite id with a new GUID to ensure it is unique in the test scope.
    /// </summary>
    public static NotificationOrder GetSmsNotificationForOneReservedRecipient()
    {
        return new NotificationOrder()
        {
            ResourceId = null,
            Creator = new("ttd"),
            ConditionEndpoint = null,
            IgnoreReservation = false,
            Created = DateTime.UtcNow,
            Type = OrderType.Notification,
            SendersReference = "local-testing",
            RequestedSendTime = DateTime.UtcNow,
            NotificationChannel = NotificationChannel.Sms,
            SendingTimePolicy = SendingTimePolicy.Daytime,

            Templates =
            [
                new SmsTemplate()
                {
                    Body = "sms-body",
                    SenderNumber = "Altinn local test"
                }
            ],

            Recipients =
            [
                new Recipient()
                {
                    IsReserved = true,
                    OrganizationNumber = null,
                    NationalIdentityNumber = null,

                    AddressInfo =
                    [
                        new SmsAddressPoint()
                        {
                            MobileNumber = "+4799999999",
                            AddressType = AddressType.Sms
                        }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// NOTE! Overwrite id with a new GUID to ensure it is unique in the test scope.
    /// </summary>
    public static NotificationOrder GetEmailNotificationForOneReservedRecipient()
    {
        return new NotificationOrder()
        {
            ResourceId = null,
            Creator = new("ttd"),
            ConditionEndpoint = null,
            IgnoreReservation = false,
            Created = DateTime.UtcNow,
            Type = OrderType.Notification,
            SendersReference = "local-testing",
            RequestedSendTime = DateTime.UtcNow,
            NotificationChannel = NotificationChannel.Sms,
            SendingTimePolicy = SendingTimePolicy.Daytime,

            Templates =
            [
                new EmailTemplate()
                {
                    Body = "email-body",
                    Subject = "email-subject",
                    FromAddress = "sender@domain.com",
                    ContentType = EmailContentType.Html
                }
            ],
            Recipients =
            [
                new Recipient()
                {
                    IsReserved = true,
                    OrganizationNumber = null,
                    NationalIdentityNumber = null,

                    AddressInfo =
                    [
                        new EmailAddressPoint()
                        {
                            AddressType = AddressType.Email,
                            EmailAddress = "recipient1@domain.com"
                        }
                    ],
                }
            ]
        };
    }

    public static (NotificationOrder Order, SmsNotification Notification) GetSmsNotificationOrderForReservedRecipient()
    {
        NotificationOrder order = GetSmsNotificationForOneReservedRecipient();
        order.Id = Guid.NewGuid();

        var recipient = order.Recipients[0];
        SmsAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Sms) as SmsAddressPoint;

        var smsNotification = new SmsNotification()
        {
            OrderId = order.Id,
            Id = Guid.NewGuid(),
            RequestedSendTime = order.RequestedSendTime,
            SendResult = new(SmsNotificationResultType.Failed_RecipientReserved, DateTime.UtcNow),
            Recipient = new()
            {
                MobileNumber = addressPoint!.MobileNumber,
            }
        };

        return (order, smsNotification);
    }

    public static (NotificationOrder Order, EmailNotification Notification) GetEmailNotificationOrderForReservedRecipient()
    {
        NotificationOrder order = GetEmailNotificationForOneReservedRecipient();
        order.Id = Guid.NewGuid();

        var recipient = order.Recipients[0];
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        var emailNotification = new EmailNotification()
        {
            OrderId = order.Id,
            Id = Guid.NewGuid(),
            RequestedSendTime = order.RequestedSendTime,
            SendResult = new(EmailNotificationResultType.Failed_RecipientReserved, DateTime.UtcNow),
            Recipient = new()
            {
                ToAddress = addressPoint!.EmailAddress
            }
        };

        return (order, emailNotification);
    }
}
