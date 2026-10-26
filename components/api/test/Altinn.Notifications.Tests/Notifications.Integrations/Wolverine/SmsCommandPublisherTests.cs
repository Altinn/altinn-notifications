using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Publishers;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class SmsCommandPublisherTests
{
    private readonly Sms _sms = new(Guid.NewGuid(), "Altinn", "+4799999999", "Test message body");

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ForwardsSameTokenToWolverinePublisher()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var wolverinePublisherMock = CreateWolverinePublisherMock();
        var publisher = new SmsCommandPublisher(wolverinePublisherMock.Object);

        // Act
        await publisher.PublishAsync(_sms, cancellationTokenSource.Token);

        // Assert
        wolverinePublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), cancellationTokenSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ValidSms_MapsAllFieldsToSendSmsCommandAndPublishesOnce()
    {
        // Arrange
        SendSmsCommand? capturedCommand = null;
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendSmsCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        var publisher = new SmsCommandPublisher(wolverinePublisherMock.Object);

        // Act
        await publisher.PublishAsync(_sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(_sms.NotificationId, capturedCommand.NotificationId);
        Assert.Equal("+4799999999", capturedCommand.MobileNumber);
        Assert.Equal("Test message body", capturedCommand.Body);
        Assert.Equal("Altinn", capturedCommand.SenderNumber);

        wolverinePublisherMock.Verify(
            m => m.PublishCommandAsync(
                It.Is<SendSmsCommand>(c => c.NotificationId == _sms.NotificationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsInvalidOperationException_PropagatesSameException()
    {
        // Arrange
        var thrown = new InvalidOperationException("Service Bus unavailable");
        var publisher = new SmsCommandPublisher(CreateThrowingWolverinePublisherMock(thrown).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(_sms, TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_PropagatesSameException()
    {
        // Arrange
        var thrown = new OperationCanceledException();
        var publisher = new SmsCommandPublisher(CreateThrowingWolverinePublisherMock(thrown).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_sms, TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsContentTooLarge_ThrowsNotificationSizeLimitExceededWithInnerException()
    {
        // Arrange
        var tooLarge = new ContentTooLargeException("too large", new InvalidOperationException("inner"));
        var publisher = new SmsCommandPublisher(CreateThrowingWolverinePublisherMock(tooLarge).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotificationSizeLimitExceededException>(
            () => publisher.PublishAsync(_sms, TestContext.Current.CancellationToken));

        Assert.Same(tooLarge, exception.InnerException);
        Assert.Contains(_sms.NotificationId.ToString(), exception.Message);
    }

    private static Mock<IWolverinePublisher> CreateWolverinePublisherMock()
    {
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return wolverinePublisherMock;
    }

    private static Mock<IWolverinePublisher> CreateThrowingWolverinePublisherMock(Exception exception)
    {
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        return wolverinePublisherMock;
    }
}
