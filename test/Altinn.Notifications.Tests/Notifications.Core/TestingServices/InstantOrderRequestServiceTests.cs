using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class InstantOrderRequestServiceTests
{
    [Fact]
    public async Task RetrieveTrackingInformation_WhenInstantNotificationOrderDoesNotExist_ReturnsNull()
    {
        // Arrange
        string creatorName = "non-existent-creator-short-name";
        string idempotencyId = "non-existent-idempotency-identifier";

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object);

        // Act
        var result = await service.RetrieveTrackingInformation(creatorName, idempotencyId);

        // Assert
        Assert.Null(result);

        orderRepositoryMock.Verify(e => e.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WhenInstantNotificationOrderExists_ReturnsTrackingInfo()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";
        string sendersReference = "test-sender-reference";

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
        orderRepositoryMock.Setup(r => r.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, It.IsAny<CancellationToken>())).ReturnsAsync(expectedTracking);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object);

        // Act
        var result = await service.RetrieveTrackingInformation(creatorName, idempotencyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);

        orderRepositoryMock.Verify(r => r.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, token) => token.ThrowIfCancellationRequested())
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RetrieveTrackingInformation(creatorName, idempotencyId, cancellationTokenSource.Token));

        orderRepositoryMock.Verify(r => r.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, It.Is<CancellationToken>(token => token.IsCancellationRequested)), Times.Once);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenRepositoryReturnsNull_ReturnsNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();

        var orderCreationDateTime = DateTime.UtcNow;
        var creatorShortName = "creator-short-name";
        var sendersReference = "207B08E2-814A-4479-9509-8DCA45A64401";

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            SendersReference = sendersReference,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
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
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.Is<InstantNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
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
        var result = await service.PersistInstantSmsNotificationAsync(instantNotificationOrder);

        // Assert
        Assert.Null(result);

        guidServiceMock.Verify(e => e.NewGuid(), Times.Once);
        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);

        orderRepositoryMock.Verify(
             e => e.Create(
                It.Is<InstantNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
                It.IsAny<CancellationToken>()),
             Times.Once);

        shortMessageServiceClient.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Never);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenPersistenceIsSuccessful_ReturnsTrackingInformation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();

        var orderCreationDateTime = DateTime.UtcNow;
        var creatorShortName = "creator-short-name";
        var sendersReference = "207B08E2-814A-4479-9509-8DCA45A64401";

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            SendersReference = sendersReference,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
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
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(
            e => e.Create(
                It.Is<InstantNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstantNotificationOrderTracking()
            {
                OrderChainId = orderChainId,
                Notification = new NotificationOrderChainShipment
                {
                    ShipmentId = orderId,
                    SendersReference = sendersReference
                }
            });

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(smsOrderId);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(orderCreationDateTime);

        var taskCompletionSource = new TaskCompletionSource();
        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClient
            .Setup(e => e.SendAsync(It.Is<ShortMessage>(e => e.NotificationId == smsOrderId)))
            .Callback(() => taskCompletionSource.SetResult())
            .ReturnsAsync(new ShortMessageSendResult()
            {
                Success = true,
                ErrorDetails = null,
                StatusCode = System.Net.HttpStatusCode.OK
            });

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object);

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantNotificationOrder);

        // Wait for the short message to be sent.
        await taskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);

        guidServiceMock.Verify(e => e.NewGuid(), Times.Once);
        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Once);
        shortMessageServiceClient.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Once);

        orderRepositoryMock.Verify(
             e => e.Create(
                It.Is<InstantNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
                It.IsAny<CancellationToken>()),
             Times.Once);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();

        var orderCreationDateTime = DateTime.UtcNow;
        var creatorShortName = "creator-short-name";
        var sendersReference = "207B08E2-814A-4479-9509-8DCA45A64401";

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            SendersReference = sendersReference,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
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
            }
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.Is<InstantNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
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

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await service.PersistInstantSmsNotificationAsync(instantNotificationOrder, cancellationTokenSource.Token));

        guidServiceMock.Verify(e => e.NewGuid(), Times.Never);
        dateTimeServiceMock.Verify(e => e.UtcNow(), Times.Never);
        shortMessageServiceClient.Verify(e => e.SendAsync(It.IsAny<ShortMessage>()), Times.Never);

        orderRepositoryMock.Verify(
             e => e.Create(
                It.Is<InstantNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.Is<SmsNotification>(e => e.Id == smsOrderId),
                It.Is<DateTime>(e => e == orderCreationDateTime.AddSeconds(3600)),
                It.Is<int>(e => e == 1),
                It.IsAny<CancellationToken>()),
             Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task PersistInstantSmsNotificationAsync_PassesValidObjectsToRepositoryAndSendingClient_BasedOnInstantNotificationOrder(string? senderIdentifier)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var smsOrderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var orderCreationDateTime = DateTime.UtcNow;

        var defaultSmsSenderIdentifier = "Altinn";
        var creatorShortName = "creator-short-name";
        var longMessageContent = new string('a', 2500);
        var sendersReference = "207B08E2-814A-4479-9509-8DCA45A64401";

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            OrderChainId = orderChainId,
            Created = orderCreationDateTime,
            SendersReference = sendersReference,
            Creator = new Creator(creatorShortName),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    TimeToLiveInSeconds = 3600,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = senderIdentifier,
                        Message = longMessageContent
                    }
                }
            }
        };

        int? initiatedSmsMessageCount = null;
        ShortMessage? initiatedShortMessage = null;
        DateTime? initiatedSmsExpiryDateTime = null;
        SmsNotification? initiatedSmsNotification = null;
        NotificationOrder? initiatedNotificationOrder = null;

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(
            e => e.Create(
                It.Is<InstantNotificationOrder>(e => e.OrderChainId == orderChainId),
                It.Is<NotificationOrder>(e => e.Id == orderId),
                It.IsAny<SmsNotification>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback((InstantNotificationOrder instantNotificationOrder, NotificationOrder notificationOrder, SmsNotification smsNotification, DateTime smsExpiryDateTime, int smsMessageCount, CancellationToken cancellationToken) =>
            {
                initiatedSmsMessageCount = smsMessageCount;
                initiatedSmsNotification = smsNotification;
                initiatedNotificationOrder = notificationOrder;
                initiatedSmsExpiryDateTime = smsExpiryDateTime;
            })
            .ReturnsAsync(new InstantNotificationOrderTracking()
            {
                OrderChainId = orderChainId,
                Notification = new NotificationOrderChainShipment
                {
                    ShipmentId = orderId,
                    SendersReference = sendersReference
                }
            });

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(smsOrderId);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(orderCreationDateTime);

        var taskCompletionSource = new TaskCompletionSource();
        var shortMessageServiceClient = new Mock<IShortMessageServiceClient>();
        shortMessageServiceClient
            .Setup(e => e.SendAsync(It.IsAny<ShortMessage>()))
            .Callback((ShortMessage shortMessage) =>
            {
                initiatedShortMessage = shortMessage;
                taskCompletionSource.SetResult();
            })
            .ReturnsAsync(new ShortMessageSendResult()
            {
                Success = true,
                ErrorDetails = null,
                StatusCode = System.Net.HttpStatusCode.OK
            });

        var service = GetTestService(
            guidService: guidServiceMock.Object,
            dateTimeService: dateTimeServiceMock.Object,
            orderRepository: orderRepositoryMock.Object,
            shortMessageServiceClient: shortMessageServiceClient.Object);

        // Act
        await service.PersistInstantSmsNotificationAsync(instantNotificationOrder);

        // Wait for the short message to be sent.
        await taskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(16, initiatedSmsMessageCount);

        Assert.NotNull(initiatedSmsExpiryDateTime);
        Assert.Equal(orderCreationDateTime.AddSeconds(3600), initiatedSmsExpiryDateTime);

        Assert.NotNull(initiatedSmsNotification);
        Assert.NotNull(initiatedSmsNotification.Recipient);
        Assert.NotNull(initiatedSmsNotification.SendResult);
        Assert.NotEqual(orderId, initiatedSmsNotification.Id);
        Assert.Equal(orderId, initiatedSmsNotification.OrderId);
        Assert.NotEqual(orderChainId, initiatedSmsNotification.Id);
        Assert.Equal(orderCreationDateTime, initiatedSmsNotification.RequestedSendTime);
        Assert.Equal(NotificationChannel.Sms, initiatedSmsNotification.NotificationChannel);
        Assert.Equal(orderCreationDateTime, initiatedSmsNotification.SendResult.ResultTime);
        Assert.Null(initiatedSmsNotification.Recipient.IsReserved);
        Assert.Null(initiatedSmsNotification.Recipient.CustomizedBody);
        Assert.Null(initiatedSmsNotification.Recipient.OrganizationNumber);
        Assert.Null(initiatedSmsNotification.Recipient.NationalIdentityNumber);

        Assert.NotNull(initiatedNotificationOrder);
        Assert.Null(initiatedNotificationOrder.ResourceId);
        Assert.Equal(orderId, initiatedNotificationOrder.Id);
        Assert.Null(initiatedNotificationOrder.ConditionEndpoint);
        Assert.Null(initiatedNotificationOrder.IgnoreReservation);
        Assert.NotEqual(orderChainId, initiatedNotificationOrder.Id);
        Assert.Equal(OrderType.Instant, initiatedNotificationOrder.Type);
        Assert.Equal(orderCreationDateTime, initiatedNotificationOrder.Created);
        Assert.Equal(sendersReference, initiatedNotificationOrder.SendersReference);
        Assert.Equal(creatorShortName, initiatedNotificationOrder.Creator.ShortName);
        Assert.Equal(orderCreationDateTime, initiatedNotificationOrder.RequestedSendTime);
        Assert.Equal(NotificationChannel.Sms, initiatedNotificationOrder.NotificationChannel);
        Assert.Equal(SendingTimePolicy.Anytime, initiatedNotificationOrder.SendingTimePolicy);

        Assert.NotNull(initiatedNotificationOrder.Templates);
        Assert.Single(initiatedNotificationOrder.Templates);
        var smsTemplate = Assert.IsType<SmsTemplate>(initiatedNotificationOrder.Templates[0]);
        Assert.Equal(longMessageContent, smsTemplate.Body);
        Assert.Equal(NotificationTemplateType.Sms, smsTemplate.Type);
        Assert.Equal(defaultSmsSenderIdentifier, smsTemplate.SenderNumber);

        Assert.NotNull(initiatedNotificationOrder.Recipients);
        Assert.Single(initiatedNotificationOrder.Recipients);
        var recipient = initiatedNotificationOrder.Recipients[0];
        Assert.Null(recipient.IsReserved);
        Assert.Null(recipient.OrganizationNumber);
        Assert.Null(recipient.NationalIdentityNumber);
        Assert.NotNull(recipient.AddressInfo);
        Assert.Single(recipient.AddressInfo);
        Assert.Equal(AddressType.Sms, recipient.AddressInfo[0].AddressType);

        Assert.NotNull(initiatedShortMessage);
        Assert.Equal(3600, initiatedShortMessage.TimeToLive);
        Assert.Equal("+4799999999", initiatedShortMessage.Recipient);
        Assert.Equal(smsOrderId, initiatedShortMessage.NotificationId);
        Assert.Equal(longMessageContent, initiatedShortMessage.Message);
        Assert.Equal(defaultSmsSenderIdentifier, initiatedShortMessage.Sender);
    }

    private static InstantOrderRequestService GetTestService(IGuidService? guidService = null, IDateTimeService? dateTimeService = null, IOrderRepository? orderRepository = null, IShortMessageServiceClient? shortMessageServiceClient = null)
    {
        guidService ??= Mock.Of<IGuidService>();
        dateTimeService ??= Mock.Of<IDateTimeService>();
        orderRepository ??= Mock.Of<IOrderRepository>();
        shortMessageServiceClient ??= Mock.Of<IShortMessageServiceClient>();

        var configurationOptions = Options.Create<NotificationConfig>(new()
        {
            DefaultSmsSenderNumber = "Altinn",
            DefaultEmailFromAddress = "noreply@altinn.no"
        });

        return new InstantOrderRequestService(guidService, dateTimeService, orderRepository, configurationOptions, shortMessageServiceClient);
    }
}
