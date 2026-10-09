using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

/// <summary>
/// Unit tests for <see cref="SendSmsCommandPublisher"/>.
/// </summary>
public class SendSmsCommandPublisherTests
{
    private readonly Sms _sms = new(Guid.NewGuid(), "Altinn", "+4799999999", "Test message body");

    [Fact]
    public async Task PublishAsync_MessageBusThrowsInvalidOperationException()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(_sms, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_sms, cts.Token));

        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_sms, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_ValidSms_MapsAllFieldsCorrectlyToSendSmsCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var sms = new Sms(notificationId, "TestSender", "+4791234567", "Hello World");

        SendSmsCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendSmsCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("+4791234567", capturedCommand.MobileNumber);
        Assert.Equal("Hello World", capturedCommand.Body);
        Assert.Equal("TestSender", capturedCommand.SenderNumber);
        Assert.Equal(notificationId, capturedCommand.NotificationId);
        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static SendSmsCommandPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock)
    {
        return new SendSmsCommandPublisher(messageBusPublisherMock.Object);
    }
}
