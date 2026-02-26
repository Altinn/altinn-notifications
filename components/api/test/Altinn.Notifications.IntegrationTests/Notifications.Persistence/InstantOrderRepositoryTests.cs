using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

/// <summary>
/// Integration tests for OrderRepository focusing on instant notification functionality with flattened structures.
/// </summary>
public class InstantOrderRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete;
    private readonly List<Guid> _ordersChainIdsToDelete;

    public InstantOrderRepositoryTests()
    {
        _orderIdsToDelete = [];
        _ordersChainIdsToDelete = [];
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_orderIdsToDelete.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
            await PostgreUtil.RunSql(deleteSql);
        }

        if (_ordersChainIdsToDelete.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orderschain oc where oc.orderid in ('{string.Join("','", _ordersChainIdsToDelete)}')";
            await PostgreUtil.RunSql(deleteSql);
        }
    }

    [Fact]
    public async Task Create_InstantSmsNotificationOrder_ReturnsTrackingInformation()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var smsNotificationId = Guid.NewGuid();
        var creationDateTime = DateTime.UtcNow;
        var phoneNumber = "+4799999999";
        var messageBody = "Test SMS message for flattened structure";
        var senderNumber = "TestSender";
        var creatorShortName = "ttd";
        var sendersReference = $"test-ref-{Guid.NewGuid():N}";
        var idempotencyId = $"idempotency-{Guid.NewGuid():N}";
        var timeToLiveSeconds = 3600;
        var smsMessageCount = 1;

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = creationDateTime,
            Creator = new Creator(creatorShortName),
            SendersReference = sendersReference,
            IdempotencyId = idempotencyId,
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = timeToLiveSeconds,
                ShortMessageContent = new ShortMessageContent
                {
                    Message = messageBody,
                    Sender = senderNumber
                }
            }
        };

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Creator = new Creator(creatorShortName),
            Created = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = creationDateTime,
            SendersReference = sendersReference,
            Recipients = [new([new SmsAddressPoint(phoneNumber)])],
            Templates = [new SmsTemplate(senderNumber, messageBody)]
        };

        var smsNotification = new SmsNotification
        {
            Id = smsNotificationId,
            OrderId = orderId,
            RequestedSendTime = creationDateTime,
            Recipient = new SmsRecipient { MobileNumber = phoneNumber },
            SendResult = new(SmsNotificationResultType.Sending, creationDateTime)
        };

        var expiryDateTime = creationDateTime.AddSeconds(timeToLiveSeconds);

        // Act
        var result = await repository.Create(
            instantSmsNotificationOrder,
            notificationOrder,
            smsNotification,
            expiryDateTime,
            smsMessageCount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);
    }

    [Fact]
    public async Task Create_InstantEmailNotificationOrder_ReturnsTrackingInformation()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var emailNotificationId = Guid.NewGuid();
        var creationDateTime = DateTime.UtcNow;
        var emailAddress = "test@example.com";
        var subject = "Test Email Subject";
        var body = "Test email body for flattened structure";
        var fromAddress = "sender@altinn.no";
        var creatorShortName = "ttd";
        var sendersReference = $"test-email-ref-{Guid.NewGuid():N}";
        var idempotencyId = $"email-idempotency-{Guid.NewGuid():N}";

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantEmailNotificationOrder = new InstantEmailNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = creationDateTime,
            Creator = new Creator(creatorShortName),
            SendersReference = sendersReference,
            IdempotencyId = idempotencyId,
            InstantEmailDetails = new InstantEmailDetails
            {
                EmailAddress = emailAddress,
                EmailContent = new InstantEmailContent
                {
                    Subject = subject,
                    Body = body,
                    ContentType = EmailContentType.Plain,
                    FromAddress = fromAddress
                }
            }
        };

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Creator = new Creator(creatorShortName),
            Created = creationDateTime,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = creationDateTime,
            SendersReference = sendersReference,
            Recipients = [new([new EmailAddressPoint(emailAddress)])],
            Templates = [new EmailTemplate(fromAddress, subject, body, EmailContentType.Plain)]
        };

        var emailNotification = new EmailNotification
        {
            Id = emailNotificationId,
            OrderId = orderId,
            RequestedSendTime = creationDateTime,
            Recipient = new EmailRecipient { ToAddress = emailAddress },
            SendResult = new(EmailNotificationResultType.Sending, creationDateTime)
        };

        var emailExpiryDateTime = creationDateTime.AddHours(48);

        // Act
        var result = await repository.Create(
            instantEmailNotificationOrder,
            notificationOrder,
            emailNotification,
            emailExpiryDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);
    }

    [Fact]
    public async Task Create_InstantSmsNotificationOrder_WithLongMessage_CalculatesCorrectMessageCount()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var smsNotificationId = Guid.NewGuid();
        var creationDateTime = DateTime.UtcNow;
        var phoneNumber = "+4799999999";
        var longMessageBody = new string('a', 500); // Long message that requires multiple SMS
        var senderNumber = "TestSender";
        var creatorShortName = "ttd";
        var timeToLiveSeconds = 3600;
        var expectedSmsMessageCount = InstantOrderRequestService.CalculateNumberOfMessages(longMessageBody);

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = creationDateTime,
            Creator = new Creator(creatorShortName),
            IdempotencyId = $"long-message-test-{Guid.NewGuid()}",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = timeToLiveSeconds,
                ShortMessageContent = new ShortMessageContent
                {
                    Message = longMessageBody,
                    Sender = senderNumber
                }
            }
        };

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Creator = new Creator(creatorShortName),
            Created = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = creationDateTime,
            Recipients = [new([new SmsAddressPoint(phoneNumber)])],
            Templates = [new SmsTemplate(senderNumber, longMessageBody)]
        };

        var smsNotification = new SmsNotification
        {
            Id = smsNotificationId,
            OrderId = orderId,
            RequestedSendTime = creationDateTime,
            Recipient = new SmsRecipient { MobileNumber = phoneNumber },
            SendResult = new(SmsNotificationResultType.Sending, creationDateTime)
        };

        var expiryDateTime = creationDateTime.AddSeconds(timeToLiveSeconds);

        // Act
        var result = await repository.Create(
            instantSmsNotificationOrder,
            notificationOrder,
            smsNotification,
            expiryDateTime,
            expectedSmsMessageCount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
    }

    [Fact]
    public async Task Create_InstantEmailNotificationOrder_WithHtmlContent_HandlesCorrectly()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var emailNotificationId = Guid.NewGuid();
        var creationDateTime = DateTime.UtcNow;
        var emailAddress = "test@example.com";
        var subject = "HTML Email Test";
        var htmlBody = "<html><body><h1>Test</h1><p>HTML email content</p></body></html>";
        var fromAddress = "sender@altinn.no";
        var creatorShortName = "ttd";

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantEmailNotificationOrder = new InstantEmailNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = creationDateTime,
            Creator = new Creator(creatorShortName),
            IdempotencyId = $"html-email-test-{Guid.NewGuid()}",
            InstantEmailDetails = new InstantEmailDetails
            {
                EmailAddress = emailAddress,
                EmailContent = new InstantEmailContent
                {
                    Subject = subject,
                    Body = htmlBody,
                    ContentType = EmailContentType.Html,
                    FromAddress = fromAddress
                }
            }
        };

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Creator = new Creator(creatorShortName),
            Created = creationDateTime,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = creationDateTime,
            Recipients = [new([new EmailAddressPoint(emailAddress)])],
            Templates = [new EmailTemplate(fromAddress, subject, htmlBody, EmailContentType.Html)]
        };

        var emailNotification = new EmailNotification
        {
            Id = emailNotificationId,
            OrderId = orderId,
            RequestedSendTime = creationDateTime,
            Recipient = new EmailRecipient { ToAddress = emailAddress },
            SendResult = new(EmailNotificationResultType.Sending, creationDateTime)
        };

        var emailExpiryDateTime = creationDateTime.AddHours(48);

        // Act
        var result = await repository.Create(
            instantEmailNotificationOrder,
            notificationOrder,
            emailNotification,
            emailExpiryDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
    }

    [Fact]
    public async Task Create_InstantSmsNotificationOrder_WithNullSender_HandlesCorrectly()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var smsNotificationId = Guid.NewGuid();
        var creationDateTime = DateTime.UtcNow;
        var phoneNumber = "+4799999999";
        var messageBody = "Test SMS with null sender";
        var defaultSender = "Altinn"; // This would come from configuration
        var creatorShortName = "ttd";
        var timeToLiveSeconds = 3600;

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = creationDateTime,
            Creator = new Creator(creatorShortName),
            IdempotencyId = $"null-sender-test-{Guid.NewGuid()}",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = timeToLiveSeconds,
                ShortMessageContent = new ShortMessageContent
                {
                    Message = messageBody,
                    Sender = null // Null sender should use default
                }
            }
        };

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Creator = new Creator(creatorShortName),
            Created = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = creationDateTime,
            Recipients = [new([new SmsAddressPoint(phoneNumber)])],
            Templates = [new SmsTemplate(defaultSender, messageBody)] // Use default sender in template
        };

        var smsNotification = new SmsNotification
        {
            Id = smsNotificationId,
            OrderId = orderId,
            RequestedSendTime = creationDateTime,
            Recipient = new SmsRecipient { MobileNumber = phoneNumber },
            SendResult = new(SmsNotificationResultType.Sending, creationDateTime)
        };

        var expiryDateTime = creationDateTime.AddSeconds(timeToLiveSeconds);

        // Act
        var result = await repository.Create(
            instantSmsNotificationOrder,
            notificationOrder,
            smsNotification,
            expiryDateTime,
            1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
    }

    [Fact]
    public async Task Create_InstantEmailNotificationOrder_WithNullFromAddress_HandlesCorrectly()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var emailNotificationId = Guid.NewGuid();
        var creationDateTime = DateTime.UtcNow;
        var emailAddress = "test@example.com";
        var subject = "Email with null sender";
        var body = "Email content with default sender";
        var defaultFromAddress = "noreply@altinn.no"; // This would come from configuration
        var creatorShortName = "ttd";

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantEmailNotificationOrder = new InstantEmailNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = creationDateTime,
            Creator = new Creator(creatorShortName),
            IdempotencyId = $"null-email-sender-test-{Guid.NewGuid()}",
            InstantEmailDetails = new InstantEmailDetails
            {
                EmailAddress = emailAddress,
                EmailContent = new InstantEmailContent
                {
                    Subject = subject,
                    Body = body,
                    ContentType = EmailContentType.Plain,
                    FromAddress = null // Null sender should use default
                }
            }
        };

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Creator = new Creator(creatorShortName),
            Created = creationDateTime,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = creationDateTime,
            Recipients = [new([new EmailAddressPoint(emailAddress)])],
            Templates = [new EmailTemplate(defaultFromAddress, subject, body, EmailContentType.Plain)] // Use default sender
        };

        var emailNotification = new EmailNotification
        {
            Id = emailNotificationId,
            OrderId = orderId,
            RequestedSendTime = creationDateTime,
            Recipient = new EmailRecipient { ToAddress = emailAddress },
            SendResult = new(EmailNotificationResultType.Sending, creationDateTime)
        };

        var emailExpiryDateTime = creationDateTime.AddHours(48);

        // Act
        var result = await repository.Create(
            instantEmailNotificationOrder,
            notificationOrder,
            emailNotification,
            emailExpiryDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
    }

    [Fact]
    public async Task RetrieveInstantOrderTrackingInformation_ForExistingOrder_ReturnsTrackingInfo()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var creatorName = $"test-creator-{Guid.NewGuid():N}";
        var idempotencyId = Guid.NewGuid().ToString();

        // First create an order
        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var smsNotificationId = Guid.NewGuid();
        var creationDateTime = DateTime.UtcNow;
        var sendersReference = $"tracking-test-ref-{Guid.NewGuid():N}";

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = creationDateTime,
            Creator = new Creator(creatorName),
            SendersReference = sendersReference,
            IdempotencyId = idempotencyId,
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContent
                {
                    Message = "Tracking test message",
                    Sender = "TestSender"
                }
            }
        };

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Creator = new Creator(creatorName),
            Created = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = creationDateTime,
            SendersReference = sendersReference,
            Recipients = [new([new SmsAddressPoint("+4799999999")])],
            Templates = [new SmsTemplate("TestSender", "Tracking test message")]
        };

        var smsNotification = new SmsNotification
        {
            Id = smsNotificationId,
            OrderId = orderId,
            RequestedSendTime = creationDateTime,
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.Sending, creationDateTime)
        };

        await repository.Create(
            instantSmsNotificationOrder,
            notificationOrder,
            smsNotification,
            creationDateTime.AddSeconds(3600),
            1);

        // Act
        var result = await repository.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);
    }

    [Fact]
    public async Task RetrieveInstantOrderTrackingInformation_ForNonExistentOrder_ReturnsNull()
    {
        // Arrange
        OrderRepository repository = (OrderRepository)ServiceUtil
            .GetServices(new List<Type>() { typeof(IOrderRepository) })
            .First(i => i.GetType() == typeof(OrderRepository));

        var creatorName = "non-existent-creator";
        var idempotencyId = "non-existent-id";

        // Act
        var result = await repository.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId);

        // Assert
        Assert.Null(result);
    }
}
