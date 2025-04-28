using System.Data;

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
    public async Task GetDeliveryManifestAsync_WhenOrderDoesntExist_ReturnsNull()
    {
        // Arrange
        string creator = "TEST_ORG";
        Guid nonExistentOrderId = Guid.NewGuid();

        // Act
        NotificationDeliveryManifestRepository deliveryManifestRepository = (NotificationDeliveryManifestRepository)ServiceUtil.GetServices([typeof(INotificationDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(NotificationDeliveryManifestRepository));

        INotificationDeliveryManifest? deliveryManifest =
            await deliveryManifestRepository.GetDeliveryManifestAsync(nonExistentOrderId, creator, CancellationToken.None);

        // Assert
        Assert.Null(deliveryManifest);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenCreatorNameDoesntMatch_ReturnsNull()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        string creator = "TEST_ORG";
        string wrongCreator = "WRONG_ORG";
        string senderReference = "CREATOR-MISMATCH-REF-DA3D201D8418";

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
            NotificationChannel = NotificationChannel.Email,
            Templates =
            [
                new EmailTemplate("sender@example.com", "Subject", "Body", EmailContentType.Plain)
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
            await deliveryManifestRepository.GetDeliveryManifestAsync(orderId, wrongCreator, CancellationToken.None);

        // Assert
        Assert.Null(deliveryManifest);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WithCancellation_HonorsCancellationToken()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        string creator = "TEST_ORG";

        using var cts = new CancellationTokenSource();

        // Act & Assert
        NotificationDeliveryManifestRepository deliveryManifestRepository = (NotificationDeliveryManifestRepository)ServiceUtil.GetServices([typeof(INotificationDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(NotificationDeliveryManifestRepository));

        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await deliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, cts.Token));
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WithVariousOrderStatuses_MapsStatusesCorrectly()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();
        Guid emailNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string recipientPhone = "+4799999999";
        string recipientEmail = "recipient@example.com";
        string senderReference = "ORDER-STATUS-TEST-REF-D904B29A";

        _orderIdentifiers.Add(orderId);

        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = DateTime.UtcNow,
            SendersReference = senderReference,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            RequestedSendTime = DateTime.UtcNow.AddMinutes(30),
            NotificationChannel = NotificationChannel.SmsPreferred,
            Templates =
            [
                new SmsTemplate("Test Sender", "Test SMS content"),
                new EmailTemplate("sender@example.com", "Test Subject", "Test Body", EmailContentType.Plain)
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint(recipientPhone), new EmailAddressPoint(recipientEmail)])
            ]
        };

        // Create the order and notifications
        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

        // Add SMS notification
        SmsNotification smsNotification = new()
        {
            OrderId = orderId,
            Id = smsNotificationId,
            RequestedSendTime = DateTime.UtcNow.AddMinutes(30),
            Recipient = new()
            {
                MobileNumber = recipientPhone
            }
        };

        SmsNotificationRepository smsRepository = (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));
        await smsRepository.AddNotification(smsNotification, DateTime.UtcNow.AddMinutes(45), 1);

        // Add email notification
        EmailNotification emailNotification = new()
        {
            OrderId = orderId,
            Id = emailNotificationId,
            RequestedSendTime = DateTime.UtcNow.AddMinutes(30),
            Recipient = new()
            {
                ToAddress = recipientEmail
            }
        };

        EmailNotificationRepository emailRepository = (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
        await emailRepository.AddNotification(emailNotification, DateTime.UtcNow);

        // Directly modify the order status and notification statuses in the database to test status mapping
        // Note: This is typically not recommended in production code but useful for testing
        string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'Completed' WHERE alternateid = '{orderId}'";
        string updateSmsSql = $@"UPDATE notifications.smsnotifications SET result = 'Accepted' WHERE alternateid = '{smsNotificationId}'";
        string updateEmailSql = $@"UPDATE notifications.emailnotifications SET result = 'Succeeded' WHERE alternateid = '{emailNotificationId}'";

        await PostgreUtil.RunSql(updateOrderSql);
        await PostgreUtil.RunSql(updateSmsSql);
        await PostgreUtil.RunSql(updateEmailSql);

        // Act
        NotificationDeliveryManifestRepository deliveryManifestRepository = (NotificationDeliveryManifestRepository)ServiceUtil.GetServices([typeof(INotificationDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(NotificationDeliveryManifestRepository));

        INotificationDeliveryManifest? deliveryManifest = await deliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(deliveryManifest);

        // Verify order status
        Assert.Equal(senderReference, deliveryManifest.SendersReference);
        Assert.Equal(ProcessingLifecycle.Order_Completed, deliveryManifest.Status);

        // Verify recipients collection
        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Equal(2, deliveryManifest.Recipients.Count);

        // Verify SMS recipient
        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Single(smsDeliveries);
        var smsDelivery = smsDeliveries[0];
        Assert.Equal(recipientPhone, smsDelivery.Destination);
        Assert.Equal(ProcessingLifecycle.SMS_Accepted, smsDelivery.Status);

        // Verify email recipient
        var emailDeliveries = deliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).ToList();
        Assert.Single(emailDeliveries);
        var emailDelivery = emailDeliveries[0];
        Assert.Equal(recipientEmail, emailDelivery.Destination);
        Assert.Equal(ProcessingLifecycle.Email_Succeeded, emailDelivery.Status);
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
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);
        Assert.Equal(ProcessingLifecycle.Order_Registered, deliveryManifest.Status);

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
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);
        Assert.Equal(ProcessingLifecycle.Order_Registered, deliveryManifest.Status);

        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Single(deliveryManifest.Recipients);

        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Single(smsDeliveries);

        var smsDelivery = smsDeliveries[0];
        Assert.Equal(phoneNumber, smsDelivery.Destination);
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);
        Assert.Equal(ProcessingLifecycle.SMS_New, smsDelivery.Status);

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
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);
        Assert.Equal(ProcessingLifecycle.Order_Registered, deliveryManifest.Status);

        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Single(deliveryManifest.Recipients);

        var emailDeliveries = deliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).ToList();
        Assert.Single(emailDeliveries);

        var emailDelivery = emailDeliveries[0];

        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);
        Assert.Equal(ProcessingLifecycle.Email_New, emailDelivery.Status);

        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Empty(smsDeliveries);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WithAlternatePhoneNumberFormats_CorrectlyIdentifiesSmsNotifications()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string phoneNumber = "004799999999";
        string senderReference = "PHONE-FORMAT-TEST-REF-20938FD4";

        _orderIdentifiers.Add(orderId);

        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = DateTime.UtcNow,
            SendersReference = senderReference,
            RequestedSendTime = DateTime.UtcNow.AddMinutes(30),
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Sms,
            Templates =
            [
                new SmsTemplate("Test Sender", "Test SMS message content")
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
            RequestedSendTime = DateTime.UtcNow.AddMinutes(30),
            Recipient = new()
            {
                MobileNumber = phoneNumber
            }
        };

        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

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

        Assert.NotEmpty(deliveryManifest.Recipients);

        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Single(smsDeliveries);

        var smsDelivery = smsDeliveries[0];
        Assert.Equal(phoneNumber, smsDelivery.Destination);
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
        Assert.Equal(orderId, deliveryManifest.ShipmentId);
        Assert.Equal("Notification", deliveryManifest.Type);
        Assert.True(deliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, deliveryManifest.SendersReference);
        Assert.Equal(ProcessingLifecycle.Order_Registered, deliveryManifest.Status);

        Assert.NotNull(deliveryManifest.Recipients);
        Assert.Equal(2, deliveryManifest.Recipients.Count);

        // Check Email notification
        var emailDeliveries = deliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).ToList();
        Assert.Single(emailDeliveries);

        var emailDelivery = emailDeliveries[0];
        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);
        Assert.Equal(ProcessingLifecycle.Email_New, emailDelivery.Status);

        // Check SMS notification
        var smsDeliveries = deliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Single(smsDeliveries);

        var smsDelivery = smsDeliveries[0];
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);
        Assert.Equal(recipientPhoneNumber, smsDelivery.Destination);
        Assert.Equal(ProcessingLifecycle.SMS_New, smsDelivery.Status);
    }
}
