using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Publishers;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class EmailCommandPublisherTests
{
    private readonly Email _email = new(
        Guid.NewGuid(),
        "Test Subject",
        "<p>Test Body</p>",
        "sender@altinnxyz.no",
        "recipient@altinnxyz.no",
        EmailContentType.Html);

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ForwardsSameTokenToWolverinePublisher()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var wolverinePublisherMock = CreateWolverinePublisherMock();
        var publisher = new EmailCommandPublisher(wolverinePublisherMock.Object);

        // Act
        await publisher.PublishAsync(_email, cancellationTokenSource.Token);

        // Assert
        wolverinePublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), cancellationTokenSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ValidEmail_MapsAllFieldsToSendEmailCommandAndPublishesOnce()
    {
        // Arrange
        SendEmailCommand? capturedCommand = null;
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        var publisher = new EmailCommandPublisher(wolverinePublisherMock.Object);

        // Act
        await publisher.PublishAsync(_email, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(_email.NotificationId, capturedCommand.NotificationId);
        Assert.Equal("Test Subject", capturedCommand.Subject);
        Assert.Equal("<p>Test Body</p>", capturedCommand.Body);
        Assert.Equal("sender@altinnxyz.no", capturedCommand.FromAddress);
        Assert.Equal("recipient@altinnxyz.no", capturedCommand.ToAddress);
        Assert.Equal(EmailContentType.Html.ToString(), capturedCommand.ContentType);

        wolverinePublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsContentTooLarge_ThrowsNotificationSizeLimitExceededWithInnerException()
    {
        // Arrange
        var tooLarge = new ContentTooLargeException("too large", new InvalidOperationException("inner"));
        var publisher = new EmailCommandPublisher(CreateThrowingWolverinePublisherMock(tooLarge).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotificationSizeLimitExceededException>(
            () => publisher.PublishAsync(_email, TestContext.Current.CancellationToken));

        Assert.Same(tooLarge, exception.InnerException);
        Assert.Contains(_email.NotificationId.ToString(), exception.Message);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOtherException_PropagatesSameException()
    {
        // Arrange
        var thrown = new TimeoutException("Service Bus unavailable");
        var publisher = new EmailCommandPublisher(CreateThrowingWolverinePublisherMock(thrown).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => publisher.PublishAsync(_email, TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_PropagatesSameException()
    {
        // Arrange
        var thrown = new OperationCanceledException();
        var publisher = new EmailCommandPublisher(CreateThrowingWolverinePublisherMock(thrown).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
    }

    private static Mock<IWolverinePublisher> CreateWolverinePublisherMock()
    {
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return wolverinePublisherMock;
    }

    private static Mock<IWolverinePublisher> CreateThrowingWolverinePublisherMock(Exception exception)
    {
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        return wolverinePublisherMock;
    }
}
