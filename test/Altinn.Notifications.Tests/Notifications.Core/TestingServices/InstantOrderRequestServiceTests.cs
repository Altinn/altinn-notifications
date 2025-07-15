using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
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
    public async Task PersistInstantSmsNotificationAsync_WhenSuccessful_ReturnsNotificationOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderChainId = Guid.NewGuid();
        var sendersReference = "207B08E2-814A-4479-9509-8DCA45A64401";

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderChainId,
            OrderChainId = orderChainId,
            SendersReference = sendersReference,
            Creator = new Creator("test-creator"),
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

        var currentTime = DateTime.UtcNow;
        var expectedNotificationOrder = new NotificationOrder
        {
            Id = orderId,
            Created = currentTime,
            RequestedSendTime = currentTime,
            SendersReference = sendersReference,
            Creator = new Creator("test-creator"),
            NotificationChannel = NotificationChannel.Sms,
            SendingTimePolicy = SendingTimePolicy.Anytime,

            Templates =
            [
                new SmsTemplate("Test sender", "Test message")
            ],

            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")])
            ],

            ResourceId = null,
            IgnoreReservation = null,
            ConditionEndpoint = null,
            Type = OrderType.Instant
        };

        var instantRepositoryMock = new Mock<IInstantOrderRepository>();
        instantRepositoryMock.Setup(r => r.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<SmsNotification>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(d => d.UtcNow()).Returns(currentTime);

        var service = new InstantOrderRequestService(
            guidServiceMock.Object,
            dateTimeServiceMock.Object,
            instantRepositoryMock.Object,
            Options.Create(new NotificationConfig()));

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantNotificationOrder);

        // Assert
        Assert.False(result.IsError);
        Assert.NotNull(result.Value);
        Assert.Equal(orderChainId, result.Value.OrderChainId);
        Assert.Equal(orderId, result.Value.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Value.Notification.SendersReference);

        instantRepositoryMock.Verify(r => r.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<SmsNotification>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenRepositoryFailsToSave_ReturnsServiceError()
    {
        // Arrange
        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            OrderChainId = Guid.NewGuid(),
            Creator = new Creator("test-creator"),
            IdempotencyId = "B60DC3D8-EE36-45BC-BE1D-D2070B19AC97",
            SendersReference = "6D6A1B44-3DE1-4E9F-91DF-CB3DDED32E7D",
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

        var currentTime = DateTime.UtcNow;

        var instantRepositoryMock = new Mock<IInstantOrderRepository>();
        instantRepositoryMock
            .Setup(r => r.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<SmsNotification>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null!);

        var guidServiceMock = new Mock<IGuidService>();
        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(d => d.UtcNow()).Returns(currentTime);

        var service = new InstantOrderRequestService(
            guidServiceMock.Object,
            dateTimeServiceMock.Object,
            instantRepositoryMock.Object,
            Options.Create(new NotificationConfig()));

        // Act
        var result = await service.PersistInstantSmsNotificationAsync(instantNotificationOrder);

        // Assert
        Assert.True(result.IsError);
        Assert.NotNull(result.Error);
        Assert.Equal(500, result.Error.ErrorCode);
        Assert.Equal("Failed to create the instant notification order.", result.Error.ErrorMessage);

        instantRepositoryMock.Verify(r => r.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<SmsNotification>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            OrderChainId = Guid.NewGuid(),
            Creator = new Creator("test-creator"),
            IdempotencyId = "E95830A1-6A56-4C0E-84C2-F399604222DB",
            SendersReference = "590349E9-4153-40A9-A5D8-4A0C4947B3B0",
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

        var guidServiceMock = new Mock<IGuidService>();
        var dateTimeServiceMock = new Mock<IDateTimeService>();
        var instantRepositoryMock = new Mock<IInstantOrderRepository>();

        var service = new InstantOrderRequestService(
            guidServiceMock.Object,
            dateTimeServiceMock.Object,
            instantRepositoryMock.Object,
            Options.Create(new NotificationConfig()));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await service.PersistInstantSmsNotificationAsync(instantNotificationOrder, cancellationTokenSource.Token));

        instantRepositoryMock.Verify(r => r.PersistInstantSmsNotificationAsync(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<SmsNotification>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WhenInstantNotificationOrderDoesNotExist_ReturnsNull()
    {
        // Arrange
        string idempotencyId = "non-existent-id";
        string creatorName = "non-existent-creator";

        var instantRepositoryMock = new Mock<IInstantOrderRepository>();
        instantRepositoryMock
            .Setup(r => r.RetrieveTrackingInformation(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var service = GetTestService(instantRepositoryMock.Object);

        // Act
        var result = await service.RetrieveTrackingInformation(creatorName, idempotencyId);

        // Assert
        Assert.Null(result);

        instantRepositoryMock.Verify(
            r => r.RetrieveTrackingInformation(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WhenInstantNotificationOrderExists_ReturnsTrackingInfo()
    {
        // Arrange
        Guid orderChainId = Guid.NewGuid();
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";
        var expectedTracking = new InstantNotificationOrderTracking
        {
            OrderChainId = orderChainId,
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "test-reference"
            }
        };

        var instantRepositoryMock = new Mock<IInstantOrderRepository>();
        instantRepositoryMock
            .Setup(r => r.RetrieveTrackingInformation(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTracking);

        var service = GetTestService(instantRepositoryMock.Object);

        // Act
        var result = await service.RetrieveTrackingInformation(creatorName, idempotencyId);

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(orderChainId, result.Value.OrderChainId);
        Assert.Equal(expectedTracking.Notification.ShipmentId, result.Value.Notification.ShipmentId);
        Assert.Equal(expectedTracking.Notification.SendersReference, result.Value.Notification.SendersReference);

        instantRepositoryMock.Verify(
            r => r.RetrieveTrackingInformation(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";

        var instantRepositoryMock = new Mock<IInstantOrderRepository>();
        instantRepositoryMock
            .Setup(r => r.RetrieveTrackingInformation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, token) => token.ThrowIfCancellationRequested())
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var service = GetTestService(instantRepositoryMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RetrieveTrackingInformation(creatorName, idempotencyId, cancellationTokenSource.Token));

        instantRepositoryMock.Verify(
            r => r.RetrieveTrackingInformation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<CancellationToken>(token => token.IsCancellationRequested)),
            Times.Once);
    }

    private static InstantOrderRequestService GetTestService(IInstantOrderRepository? instantRepositoryMock = null, Guid? uniqueIdentifier = null, DateTime? dateTime = null)
    {
        instantRepositoryMock ??= new Mock<IInstantOrderRepository>().Object;

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(e => e.UtcNow()).Returns(dateTime ?? DateTime.UtcNow);

        var guidServiceMock = new Mock<IGuidService>();
        guidServiceMock.Setup(e => e.NewGuid()).Returns(uniqueIdentifier ?? Guid.NewGuid());

        var configurationOptions = Options.Create<NotificationConfig>(new()
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "TestDefaultSmsSenderNumberNumber"
        });

        return new InstantOrderRequestService(guidServiceMock.Object, dateTimeServiceMock.Object, instantRepositoryMock, configurationOptions);
    }
}
