using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Wolverine;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class SmsSendResultPublisherTests
{
    [Fact]
    public async Task DispatchAsync_SendsCommandWithCorrectFields()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        const string gatewayReference = "link-gateway-ref-abc";
        const SmsSendResult sendResult = SmsSendResult.Accepted;

        var result = new SendOperationResult
        {
            NotificationId = notificationId,
            GatewayReference = gatewayReference,
            SendResult = sendResult
        };

        SmsSendResultCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<SmsSendResultCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SmsSendResultCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var sut = new SmsSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(notificationId, capturedCommand.NotificationId);
        Assert.Equal(gatewayReference, capturedCommand.GatewayReference);
        Assert.Equal("Accepted", capturedCommand.SendResult);
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<SmsSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenGatewayReferenceIsEmpty_CommandGatewayReferenceIsNull()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = string.Empty,
            SendResult = SmsSendResult.Failed
        };

        SmsSendResultCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<SmsSendResultCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SmsSendResultCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var sut = new SmsSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Null(capturedCommand.GatewayReference);
    }

    [Fact]
    public async Task DispatchAsync_WhenResultIsNull_ThrowsArgumentNullException()
    {
        var messageBusMock = new Mock<IMessageBus>();
        var sut = new SmsSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DispatchAsync(null!));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<SmsSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenSendResultIsNull_ThrowsArgumentException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = "ref",
            SendResult = null
        };

        var messageBusMock = new Mock<IMessageBus>();
        var sut = new SmsSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.DispatchAsync(result));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<SmsSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotificationIdIsNull_ThrowsArgumentException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = null,
            GatewayReference = "ref",
            SendResult = SmsSendResult.Accepted
        };

        var messageBusMock = new Mock<IMessageBus>();
        var sut = new SmsSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.DispatchAsync(result));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<SmsSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotificationIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.Empty,
            GatewayReference = "ref",
            SendResult = SmsSendResult.Accepted
        };

        var messageBusMock = new Mock<IMessageBus>();
        var sut = new SmsSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.DispatchAsync(result));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<SmsSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    private static ServiceProvider CreateServiceProvider(IMessageBus messageBus)
    {
        var services = new ServiceCollection();
        services.AddSingleton(messageBus);
        return services.BuildServiceProvider();
    }
}
