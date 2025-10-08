using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

/// <summary>
/// Tests for the SMS notification functionality in InstantOrderRequestService.
/// </summary>
public class InstantSmsOrderRequestServiceTests
{
    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenValidInput_ReturnsTrackingInformation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var orderCreationDateTime = DateTime.UtcNow;
        var creatorShortName = "creator-short-name";
        var sendersReference = "207B08E2-814A-4479-9509-8DCA45A64401";

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            SendersReference = sendersReference,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                TimeToLiveInSeconds = 3600,
                PhoneNumber = "+4799999999",
                ShortMessageContent = new ShortMessageContent
                {
                    Sender = "Test sender",
                    Message = "Test message"
                }
            }
        };

        var expectedTracking = new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = orderId,
                SendersReference = sendersReference
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.Is<InstantSmsNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTracking);

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(smsOrderId);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(orderCreationDateTime);

        var taskCompletionSource = new TaskCompletionSource();
        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClient
            .Setup(e => e.SendAsync(It.Is<ShortMessage>(m => m.NotificationId == smsOrderId)))
            .Callback(() => taskCompletionSource.SetResult())
            .ReturnsAsync(new ShortMessageSendResult());

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object);

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantSmsNotificationOrder);

        // Wait for the background Task.Run to complete
        await taskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);

        guidServiceMock.Verify(e => e.NewGuid(), Times.Once);
        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);

        orderRepositoryMock.Verify(
            e => e.Create(
                It.Is<InstantSmsNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        shortMessageServiceClient.Verify(
            e => e.SendAsync(
            It.Is<ShortMessage>(m =>
                m.Message == "Test message" &&
                m.NotificationId == smsOrderId &&
                m.TimeToLive == 3600 &&
                m.Recipient == "+4799999999" &&
                m.Sender == "Test sender")),
            Times.Once);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenRepositoryCreateFails_ReturnsNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var orderCreationDateTime = DateTime.UtcNow;
        var creatorShortName = "creator-short-name";

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                TimeToLiveInSeconds = 3600,
                PhoneNumber = "+4799999999",
                ShortMessageContent = new ShortMessageContent
                {
                    Sender = "Test sender",
                    Message = "Test message"
                }
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(smsOrderId);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(orderCreationDateTime);

        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClient.Setup(e => e.SendAsync(It.IsAny<ShortMessage>())).ReturnsAsync((ShortMessageSendResult?)null!);

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object);

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantSmsNotificationOrder);

        // Assert
        Assert.Null(result);

        guidServiceMock.Verify(e => e.NewGuid(), Times.Once);
        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);

        orderRepositoryMock.Verify(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Since repository creation failed, SMS client should still be called
        shortMessageServiceClient.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Never);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            OrderChainId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            Creator = new Creator("creator-short-name"),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                TimeToLiveInSeconds = 3600,
                PhoneNumber = "+4799999999",
                ShortMessageContent = new ShortMessageContent
                {
                    Sender = "Test sender",
                    Message = "Test message"
                }
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        var guidServiceMock = new Mock<IGuidService>();
        var dateTimeServiceMock = new Mock<IDateTimeService>();
        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.PersistInstantSmsNotificationAsync(instantSmsNotificationOrder, cancellationTokenSource.Token));

        guidServiceMock.Verify(e => e.NewGuid(), Times.Never);
        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        shortMessageServiceClient.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Never);
        orderRepositoryMock.Verify(
            e => e.Create(
            It.IsAny<InstantSmsNotificationOrder>(),
            It.IsAny<NotificationOrder>(),
            It.IsAny<SmsNotification>(),
            It.IsAny<DateTime>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task PersistInstantSmsNotificationAsync_WhenSenderIsNullOrEmpty_UsesDefaultSender(string? senderIdentifier)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var orderCreationDateTime = DateTime.UtcNow;
        var defaultSmsSenderIdentifier = "Altinn";
        var creatorShortName = "creator-short-name";

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                TimeToLiveInSeconds = 3600,
                PhoneNumber = "+4799999999",
                ShortMessageContent = new ShortMessageContent
                {
                    Sender = senderIdentifier,
                    Message = "Test message"
                }
            }
        };

        var expectedTracking = new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = orderId
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTracking);

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(smsOrderId);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(orderCreationDateTime);

        var taskCompletionSource = new TaskCompletionSource();
        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClient
            .Setup(e => e.SendAsync(It.Is<ShortMessage>(m => m.NotificationId == smsOrderId)))
            .Callback(() => taskCompletionSource.SetResult())
            .ReturnsAsync(new ShortMessageSendResult());

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object,
            defaultSmsSender: defaultSmsSenderIdentifier);

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantSmsNotificationOrder);

        // Wait for the background Task.Run to complete
        await taskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(result);

        // Verify that the SMS notification template uses the default sender
        orderRepositoryMock.Verify(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.Is<NotificationOrder>(order =>
                    order.Templates.OfType<SmsTemplate>().Any(smsTemplate =>
                        smsTemplate.SenderNumber == defaultSmsSenderIdentifier)),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify that the SMS client receives the default sender
        shortMessageServiceClient.Verify(
            e => e.SendAsync(
            It.Is<ShortMessage>(m => m.Sender == defaultSmsSenderIdentifier)),
            Times.Once);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenMessageIsLong_CalculatesCorrectMessageCount()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var orderCreationDateTime = DateTime.UtcNow;
        var creatorShortName = "creator-short-name";
        var longMessageContent = new string('a', 2500); // Long message that will require multiple SMS

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                TimeToLiveInSeconds = 3600,
                PhoneNumber = "+4799999999",
                ShortMessageContent = new ShortMessageContent
                {
                    Sender = "Test sender",
                    Message = longMessageContent
                }
            }
        };

        var expectedTracking = new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = orderId
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTracking);

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(smsOrderId);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(orderCreationDateTime);

        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClient.Setup(e => e.SendAsync(It.IsAny<ShortMessage>())).ReturnsAsync((ShortMessageSendResult?)null!);

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object);

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantSmsNotificationOrder);

        // Assert
        Assert.NotNull(result);

        // Verify that message count is calculated correctly (should be > 1 for long message)
        orderRepositoryMock.Verify(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.Is<int>(count => count > 1), // Long message should require multiple SMS
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenValidInput_CreatesCorrectNotificationOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var orderCreationDateTime = DateTime.UtcNow;
        var creatorShortName = "creator-short-name";
        var sendersReference = "test-reference";
        var phoneNumber = "+4799999999";
        var messageBody = "Test message";
        var senderIdentifier = "Test sender";

        var instantSmsNotificationOrder = new InstantSmsNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            SendersReference = sendersReference,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                TimeToLiveInSeconds = 3600,
                PhoneNumber = phoneNumber,
                ShortMessageContent = new ShortMessageContent
                {
                    Sender = senderIdentifier,
                    Message = messageBody
                }
            }
        };

        var expectedTracking = new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = orderId,
                SendersReference = sendersReference
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.IsAny<NotificationOrder>(),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTracking);

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(smsOrderId);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(orderCreationDateTime);

        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClient.Setup(e => e.SendAsync(It.IsAny<ShortMessage>())).ReturnsAsync((ShortMessageSendResult?)null!);

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object);

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantSmsNotificationOrder);

        // Assert
        Assert.NotNull(result);

        // Verify the NotificationOrder structure
        orderRepositoryMock.Verify(
            e => e.Create(
                It.IsAny<InstantSmsNotificationOrder>(),
                It.Is<NotificationOrder>(order =>
                    order.Id == orderId &&
                    order.Creator.ShortName == creatorShortName &&
                    order.Created == orderCreationDateTime &&
                    order.SendersReference == sendersReference &&
                    order.NotificationChannel == NotificationChannel.Sms &&
                    order.RequestedSendTime == orderCreationDateTime &&
                    order.Recipients.Count == 1 &&
                    order.Recipients[0].AddressInfo.Count == 1 &&
                    order.Templates.Count == 1),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static InstantOrderRequestService GetTestService(
        IGuidService? guidService = null,
        IDateTimeService? dateTimeService = null,
        IOrderRepository? orderRepository = null,
        IShortMessageServiceClient? shortMessageServiceClient = null,
        IInstantEmailServiceClient? instantEmailServiceClient = null,
        string? defaultSmsSender = null,
        string? defaultEmailFromAddress = null)
    {
        guidService ??= Mock.Of<IGuidService>();
        dateTimeService ??= Mock.Of<IDateTimeService>();
        orderRepository ??= Mock.Of<IOrderRepository>();
        shortMessageServiceClient ??= Mock.Of<IShortMessageServiceClient>();
        instantEmailServiceClient ??= Mock.Of<IInstantEmailServiceClient>();

        var notificationConfig = new NotificationConfig
        {
            DefaultSmsSenderNumber = defaultSmsSender ?? "Altinn",
            DefaultEmailFromAddress = defaultEmailFromAddress ?? "noreply@altinn.no"
        };

        var configurationOptions = Options.Create(notificationConfig);

        return new InstantOrderRequestService(
            guidService,
            dateTimeService,
            orderRepository,
            configurationOptions,
            shortMessageServiceClient,
            instantEmailServiceClient);
    }
}
