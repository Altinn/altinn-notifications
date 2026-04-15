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

        var emailSendResultPublisher = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await emailSendResultPublisher.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(notificationId, capturedCommand.NotificationId);
        Assert.Equal(operationId, capturedCommand.OperationId);
        Assert.Equal("Delivered", capturedCommand.SendResult);
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
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

        var emailSendResultPublisher = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await emailSendResultPublisher.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Null(capturedCommand.OperationId);
    }

    [Fact]
    public async Task DispatchAsync_WhenSendResultIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = null
        };

        var messageBusMock = new Mock<IMessageBus>();
        var emailSendResultPublisher = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => emailSendResultPublisher.DispatchAsync(result));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotificationIdIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = null,
            OperationId = "op-123",
            SendResult = EmailSendResult.Delivered
        };

        var messageBusMock = new Mock<IMessageBus>();
        var emailSendResultPublisher = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => emailSendResultPublisher.DispatchAsync(result));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenNotificationIdIsEmpty_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.Empty,
            OperationId = "op-123",
            SendResult = EmailSendResult.Delivered
        };

        var messageBusMock = new Mock<IMessageBus>();
        var emailSendResultPublisher = new EmailSendResultPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => emailSendResultPublisher.DispatchAsync(result));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<EmailSendResultCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    private static ServiceProvider CreateServiceProvider(IMessageBus messageBus)
    {
        var services = new ServiceCollection();
        services.AddSingleton(messageBus);
        return services.BuildServiceProvider();
    }
}
