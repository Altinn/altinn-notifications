using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.IntegrationTestsASB.Utils;

/// <summary>
/// Utility class for creating test data objects.
/// Ported from the existing IntegrationTests project.
/// </summary>
public static class TestdataUtil
{
    /// <summary>
    /// Creates a test order and email notification pair with new GUIDs.
    /// </summary>
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
    /// Creates a notification order with an email template and one recipient.
    /// NOTE! Overwrite id with a new GUID to ensure it is unique in the test scope.
    /// </summary>
    public static NotificationOrder NotificationOrder_EmailTemplate_OneRecipient()
    {
        return new NotificationOrder()
        {
            SendersReference = "asb-integration-test",
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
            }
        };
    }
}
