using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Publishers;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class EmailSendResultPublisherTests
{
    [Fact]
    public async Task DispatchAsync_SendsCommandWithCorrectFields()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        const string operationId = "acs-op-abc123";
        const EmailSendResult sendResult = EmailSendResult.Delivered;

        var result = new SendOperationResult
        {
            NotificationId = notificationId,
            OperationId = operationId,
            SendResult = sendResult
        };

        EmailSendResultCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<EmailSendResultCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var sut = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(notificationId, capturedCommand.NotificationId);
        Assert.Equal(operationId, capturedCommand.OperationId);
        Assert.Equal("Delivered", capturedCommand.SendResult);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotificationIdIsNull_CommandNotificationIdIsGuidEmpty()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = null,
            OperationId = "op-123",
            SendResult = EmailSendResult.Delivered
        };

        EmailSendResultCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<EmailSendResultCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var sut = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(Guid.Empty, capturedCommand.NotificationId);
    }

    [Fact]
    public async Task DispatchAsync_WhenSendResultIsNull_CommandSendResultIsEmptyString()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = null
        };

        EmailSendResultCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<EmailSendResultCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var sut = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(string.Empty, capturedCommand.SendResult);
    }

    [Fact]
    public async Task DispatchAsync_WhenOperationIdIsEmpty_CommandOperationIdIsNull()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = string.Empty,
            SendResult = EmailSendResult.Failed
        };

        EmailSendResultCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<EmailSendResultCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var sut = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Null(capturedCommand.OperationId);
    }

    [Fact]
    public async Task DispatchAsync_SendsExactlyOnce()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailSendResult.Delivered
        };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var sut = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(result);

        // Assert
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
    }

    private static IServiceProvider CreateServiceProvider(IMessageBus messageBus)
    {
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(sp => sp.GetService(typeof(IMessageBus))).Returns(messageBus);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(s => s.ServiceProvider).Returns(scopeServiceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        var rootServiceProvider = new Mock<IServiceProvider>();
        rootServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        return rootServiceProvider.Object;
    }
}
