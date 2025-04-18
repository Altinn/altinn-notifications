using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class NotificationDeliveryManifestRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdentifiers;
    private readonly List<Guid> _ordersChainIdentifiers;

    public NotificationDeliveryManifestRepositoryTests()
    {
        _orderIdentifiers = [];
        _ordersChainIdentifiers = [];
    }

    public async Task DisposeAsync()
    {
        if (_orderIdentifiers.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdentifiers)}')";
            await PostgreUtil.RunSql(deleteSql);
        }

        if (_ordersChainIdentifiers.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orderschain oc where oc.orderid in ('{string.Join("','", _ordersChainIdentifiers)}')";
            await PostgreUtil.RunSql(deleteSql);
        }
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenNoNotificationsExist_ReturnsCorrectDeliveryManifest()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string smsSender = "Test Sender";
        string emailSubject = "Test Email Subject";
        string emailBody = "Test email body content";
        string senderEmailAddress = "sender@example.com";
        string smsMessageBody = "Test SMS message content";
        string conditionEndpoint = "https://vg.no/condition";
        string senderReference = "NO-NOTIFICATIONS-ORDER-REF-30A794B67FE3";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(30);

        _orderIdentifiers.Add(orderId);

        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            SendingTimePolicy = SendingTimePolicy.Daytime,
            NotificationChannel = NotificationChannel.EmailPreferred,
            ConditionEndpoint = new Uri(conditionEndpoint),
            Templates =
            [
                new SmsTemplate(smsSender, smsMessageBody),
                new EmailTemplate(senderEmailAddress, emailSubject, emailBody, EmailContentType.Plain)
            ],
            Recipients = []
        };

        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

        // Act
        NotificationDeliveryManifestRepository deliveryManifestRepository = (NotificationDeliveryManifestRepository)ServiceUtil.GetServices([typeof(INotificationDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(NotificationDeliveryManifestRepository));

        INotificationDeliveryManifest? deliveryManifest =
            await deliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(deliveryManifest);

        Assert.NotNull(deliveryManifest.Type);
        Assert.NotEmpty(deliveryManifest.Type);
        Assert.NotNull(deliveryManifest.Status);
        Assert.NotEmpty(deliveryManifest.Status);
        Assert.Null(deliveryManifest.SequenceNumber);
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.NotNull(deliveryManifest.StatusDescription);
        Assert.NotEmpty(deliveryManifest.StatusDescription);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);

        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Empty(deliveryManifest.Recipients);

        var emailDeliveries = deliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).ToList();
        Assert.Empty(emailDeliveries);

        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Empty(smsDeliveries);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenSingleSmsNotificationExists_ReturnsCorrectDeliveryManifest()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string phoneNumber = "+4799999999";
        string senderNumber = "Test Sender";
        string messageBody = "Test SMS message content";
        string conditionEndpoint = "https://vg.no/condition";
        string senderReference = "SMS-ORDER-REF-30A794B67FE2";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(30);

        _orderIdentifiers.Add(orderId);

        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            SendingTimePolicy = SendingTimePolicy.Daytime,
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri(conditionEndpoint),
            Templates =
            [
                new SmsTemplate(messageBody, senderNumber)
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint(phoneNumber)])
            ]
        };

        SmsNotification smsNotification = new()
        {
            OrderId = orderId,
            Id = smsNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = phoneNumber
            }
        };

        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

        // Insert the SMS notification order
        SmsNotificationRepository smsRepository = (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));
        await smsRepository.AddNotification(smsNotification, DateTime.UtcNow.AddMinutes(45), 1);

        // Act
        NotificationDeliveryManifestRepository deliveryManifestRepository = (NotificationDeliveryManifestRepository)ServiceUtil.GetServices([typeof(INotificationDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(NotificationDeliveryManifestRepository));

        INotificationDeliveryManifest? deliveryManifest =
            await deliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(deliveryManifest);

        Assert.NotNull(deliveryManifest.Type);
        Assert.NotEmpty(deliveryManifest.Type);
        Assert.NotNull(deliveryManifest.Status);
        Assert.NotEmpty(deliveryManifest.Status);
        Assert.Null(deliveryManifest.SequenceNumber);
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.NotNull(deliveryManifest.StatusDescription);
        Assert.NotEmpty(deliveryManifest.StatusDescription);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);

        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Single(deliveryManifest.Recipients);

        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Single(smsDeliveries);

        var smsDelivery = smsDeliveries[0];
        Assert.NotEmpty(smsDelivery.Status);
        Assert.NotNull(smsDelivery.StatusDescription);
        Assert.Equal(phoneNumber, smsDelivery.Destination);
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);

        var emailDeliveries = deliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).ToList();
        Assert.Empty(emailDeliveries);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenSingleEmailNotificationExists_ReturnsCorrectDeliveryManifest()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid emailNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string emailSubject = "Test Email Subject";
        string emailBody = "Test email body content";
        string senderEmailAddress = "sender@example.com";
        string conditionEndpoint = "https://vg.no/condition";
        string recipientEmailAddress = "recipient@example.com";
        string senderReference = "EMAIL-ORDER-REF-30A794A67FE1";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(30);

        _orderIdentifiers.Add(orderId);

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            ConditionEndpoint = new Uri(conditionEndpoint),
            NotificationChannel = NotificationChannel.Email,
            Templates =
            [
                new EmailTemplate(senderEmailAddress, emailSubject, emailBody, EmailContentType.Plain)
            ],
            Recipients =
            [
                new Recipient([new EmailAddressPoint(recipientEmailAddress)])
            ]
        };

        EmailNotification emailNotification = new()
        {
            OrderId = orderId,
            Id = emailNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                ToAddress = recipientEmailAddress
            }
        };

        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(notificationOrder);

        EmailNotificationRepository emailRepository = (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
        await emailRepository.AddNotification(emailNotification, DateTime.UtcNow);

        // Act
        NotificationDeliveryManifestRepository deliveryManifestRepository = (NotificationDeliveryManifestRepository)ServiceUtil.GetServices([typeof(INotificationDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(NotificationDeliveryManifestRepository));

        INotificationDeliveryManifest? deliveryManifest =
            await deliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(deliveryManifest);

        Assert.NotNull(deliveryManifest.Type);
        Assert.NotEmpty(deliveryManifest.Type);
        Assert.NotNull(deliveryManifest.Status);
        Assert.NotEmpty(deliveryManifest.Status);
        Assert.Null(deliveryManifest.SequenceNumber);
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.NotNull(deliveryManifest.StatusDescription);
        Assert.NotEmpty(deliveryManifest.StatusDescription);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);

        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Single(deliveryManifest.Recipients);

        var emailDeliveries = deliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).ToList();
        Assert.Single(emailDeliveries);

        var emailDelivery = emailDeliveries[0];

        Assert.NotEmpty(emailDelivery.Status);
        Assert.NotNull(emailDelivery.StatusDescription);
        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);

        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Empty(smsDeliveries);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenBothEmailAndSmsNotificationsExist_ReturnsCorrectDeliveryManifest()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();
        Guid emailNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";

        string smsSender = "Test Sender";
        string recipientPhoneNumber = "+4799999999";
        string smsMessageBody = "Test SMS message content";

        string emailSubject = "Test Email Subject";
        string emailBody = "Test email body content";
        string senderEmailAddress = "sender@example.com";
        string recipientEmailAddress = "recipient@example.com";

        string conditionEndpoint = "https://vg.no/condition";
        string senderReference = "COMBINED-ORDER-REF-DEDBD8C568F6";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(30);

        _orderIdentifiers.Add(orderId);

        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            SendingTimePolicy = SendingTimePolicy.Daytime,
            ConditionEndpoint = new Uri(conditionEndpoint),
            NotificationChannel = NotificationChannel.EmailPreferred,
            Templates =
            [
                new SmsTemplate(smsSender, smsMessageBody),
                new EmailTemplate(senderEmailAddress, emailSubject, emailBody, EmailContentType.Plain)
            ],
            Recipients =
            [
                new Recipient(
                [
                    new SmsAddressPoint(recipientPhoneNumber),
                    new EmailAddressPoint(recipientEmailAddress)
                ])
            ]
        };

        EmailNotification emailNotification = new()
        {
            OrderId = orderId,
            Id = emailNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                ToAddress = recipientEmailAddress
            }
        };

        SmsNotification smsNotification = new()
        {
            OrderId = orderId,
            Id = smsNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = recipientPhoneNumber
            }
        };

        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

        EmailNotificationRepository emailRepository = (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
        await emailRepository.AddNotification(emailNotification, DateTime.UtcNow);

        SmsNotificationRepository smsRepository = (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));
        await smsRepository.AddNotification(smsNotification, DateTime.UtcNow.AddMinutes(45), 1);

        // Act
        NotificationDeliveryManifestRepository deliveryManifestRepository = (NotificationDeliveryManifestRepository)ServiceUtil.GetServices([typeof(INotificationDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(NotificationDeliveryManifestRepository));

        INotificationDeliveryManifest? deliveryManifest =
            await deliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(deliveryManifest);

        Assert.NotNull(deliveryManifest.Type);
        Assert.NotEmpty(deliveryManifest.Type);
        Assert.NotNull(deliveryManifest.Status);
        Assert.NotEmpty(deliveryManifest.Status);
        Assert.Null(deliveryManifest.SequenceNumber);
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.NotNull(deliveryManifest.StatusDescription);
        Assert.NotEmpty(deliveryManifest.StatusDescription);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);

        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Equal(2, deliveryManifest.Recipients.Count);

        // Check Email notification
        var emailDeliveries = deliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).ToList();
        Assert.Single(emailDeliveries);

        var emailDelivery = emailDeliveries[0];
        Assert.NotEmpty(emailDelivery.Status);
        Assert.NotNull(emailDelivery.StatusDescription);
        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);

        // Check SMS notification
        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Single(smsDeliveries);

        var smsDelivery = smsDeliveries[0];
        Assert.NotEmpty(smsDelivery.Status);
        Assert.NotNull(smsDelivery.StatusDescription);
        Assert.Equal(recipientPhoneNumber, smsDelivery.Destination);
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);
    }
}
