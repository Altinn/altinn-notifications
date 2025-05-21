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
    public static NotificationOrder NotificationOrder_SmsTemplate_OneRecipient()
    {
        return new NotificationOrder()
        {
            ResourceId = null,
            Creator = new("ttd"),
            ConditionEndpoint = null,
            IgnoreReservation = null,
            Type = OrderType.Notification,
            SendersReference = "local-testing",
            NotificationChannel = NotificationChannel.Sms,
            SendingTimePolicy = SendingTimePolicy.Daytime,
            Created = new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc),
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc),

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
                    IsReserved = null,
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
    public static NotificationOrder GetSmsNotificationForOneReservedRecipient()
    {
        return new NotificationOrder()
        {
            ResourceId = null,
            Creator = new("ttd"),
            ConditionEndpoint = null,
            IgnoreReservation = null,
            Type = OrderType.Notification,
            SendersReference = "local-testing",
            NotificationChannel = NotificationChannel.Sms,
            SendingTimePolicy = SendingTimePolicy.Daytime,
            Created = new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc),
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc),

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
    public static NotificationOrder NotificationOrder_EmailTemplate_OneRecipient()
    {
        return new NotificationOrder()
        {
            ResourceId = null,
            Creator = new("ttd"),
            ConditionEndpoint = null,
            IgnoreReservation = null,
            Type = OrderType.Notification,
            SendersReference = "local-testing",
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Email,
            Created = new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc),
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc),

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
            IgnoreReservation = null,
            Type = OrderType.Notification,
            SendersReference = "local-testing",
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Email,
            Created = new DateTime(2023, 06, 16, 08, 45, 00, DateTimeKind.Utc),
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc),

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
}
