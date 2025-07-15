using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class InstantOrderRequestServiceTests
{
    [Fact]
    public async Task RegisterInstantOrder_WhenSuccessful_ReturnsNotificationOrder()
    {
        // Arrange
        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            OrderChainId = Guid.NewGuid(),
            Creator = new Creator("test-creator"),
            IdempotencyId = "E7344199-61C7-490E-A304-1E79C488D206",
            SendersReference = "207B08E2-814A-4479-9509-8DCA45A64401",
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
            Created = currentTime,
            RequestedSendTime = currentTime,
            Id = instantNotificationOrder.OrderId,
            Creator = new Creator("test-creator"),
            NotificationChannel = NotificationChannel.Sms,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            SendersReference = "207B08E2-814A-4479-9509-8DCA45A64401",

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

        var repositoryMock = new Mock<IOrderRepository>();
        repositoryMock.Setup(r => r.Create(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<CancellationToken>())).ReturnsAsync(instantNotificationOrder);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(d => d.UtcNow()).Returns(currentTime);

        var service = new InstantOrderRequestService(
            repositoryMock.Object,
            dateTimeServiceMock.Object,
            Options.Create(new NotificationConfig()));

        // Act
        var result = await service.RegisterInstantOrder(instantNotificationOrder);

        // Assert
        Assert.False(result.IsError);
        Assert.NotNull(result.Value);
        Assert.Equal(expectedNotificationOrder.Id, result.Value.Id);
        Assert.Equal(expectedNotificationOrder.Created, result.Value.Created);
        Assert.Equal(expectedNotificationOrder.RequestedSendTime, result.Value.RequestedSendTime);
        Assert.Equal(expectedNotificationOrder.NotificationChannel, result.Value.NotificationChannel);

        repositoryMock.Verify(r => r.Create(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterInstantOrder_WhenRepositoryFailsToSave_ReturnsServiceError()
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

        var repositoryMock = new Mock<IOrderRepository>();
        repositoryMock
            .Setup(r => r.Create(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrder?)null!);

        var dateTimeServiceMock = new Mock<IDateTimeService>();
        dateTimeServiceMock.Setup(d => d.UtcNow()).Returns(currentTime);

        var service = new InstantOrderRequestService(
            repositoryMock.Object,
            dateTimeServiceMock.Object,
            Options.Create(new NotificationConfig()));

        // Act
        var result = await service.RegisterInstantOrder(instantNotificationOrder);

        // Assert
        Assert.True(result.IsError);
        Assert.NotNull(result.Error);
        Assert.Equal(500, result.Error.ErrorCode);
        Assert.Equal("Failed to create the instant notification order.", result.Error.ErrorMessage);

        repositoryMock.Verify(r => r.Create(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterInstantOrder_WhenCancellationRequested_ThrowsOperationCanceledException()
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

        var repositoryMock = new Mock<IOrderRepository>();
        var dateTimeServiceMock = new Mock<IDateTimeService>();

        var service = new InstantOrderRequestService(
            repositoryMock.Object,
            dateTimeServiceMock.Object,
            Options.Create(new NotificationConfig()));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RegisterInstantOrder(instantNotificationOrder, cancellationTokenSource.Token));

        repositoryMock.Verify(r => r.Create(It.IsAny<InstantNotificationOrder>(), It.IsAny<NotificationOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetrieveInstantOrderTracking_WhenInstantNotificationOrderDoesNotExist_ReturnsNull()
    {
        // Arrange
        string idempotencyId = "non-existent-id";
        string creatorName = "non-existent-creator";

        var repositoryMock = new Mock<IOrderRepository>();
        repositoryMock
            .Setup(r => r.GetInstantOrderTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var service = GetTestService(repositoryMock.Object);

        // Act
        var result = await service.RetrieveInstantOrderTracking(creatorName, idempotencyId);

        // Assert
        Assert.Null(result);

        repositoryMock.Verify(
            r => r.GetInstantOrderTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveInstantOrderTracking_WhenInstantNotificationOrderExists_ReturnsTrackingInfo()
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

        var repositoryMock = new Mock<IOrderRepository>();
        repositoryMock
            .Setup(r => r.GetInstantOrderTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTracking);

        var service = GetTestService(repositoryMock.Object);

        // Act
        var result = await service.RetrieveInstantOrderTracking(creatorName, idempotencyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(expectedTracking.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(expectedTracking.Notification.SendersReference, result.Notification.SendersReference);

        repositoryMock.Verify(
            r => r.GetInstantOrderTracking(
                It.Is<string>(s => s == creatorName),
                It.Is<string>(s => s == idempotencyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveInstantOrderTracking_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string creatorName = "test-creator";
        string idempotencyId = "test-idempotency-id";

        var repositoryMock = new Mock<IOrderRepository>();
        repositoryMock
            .Setup(r => r.GetInstantOrderTracking(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, token) => token.ThrowIfCancellationRequested())
            .ReturnsAsync((InstantNotificationOrderTracking?)null);

        var service = GetTestService(repositoryMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.RetrieveInstantOrderTracking(creatorName, idempotencyId, cancellationTokenSource.Token));

        repositoryMock.Verify(
            r => r.GetInstantOrderTracking(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<CancellationToken>(token => token.IsCancellationRequested)),
            Times.Once);
    }

    private static InstantOrderRequestService GetTestService(IOrderRepository? repository = null, Guid? guid = null, DateTime? dateTime = null)
    {
        if (repository == null)
        {
            var repo = new Mock<IOrderRepository>();
            repository = repo.Object;
        }

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(g => g.UtcNow())
            .Returns(dateTime ?? DateTime.UtcNow);

        var config = Options.Create<NotificationConfig>(new()
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "TestDefaultSmsSenderNumberNumber"
        });

        return new InstantOrderRequestService(repository, dateTimeMock.Object, config);
    }
}
