using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Publishers;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

/// <summary>
/// Unit tests for <see cref="ComposedEmailCommandPublisher"/>.
/// </summary>
public class ComposedEmailCommandPublisherTests
{
    private const string _sasUrl = "https://storage.example.com/container/file.pdf?sv=2021&sig=abc";

    private readonly ComposedEmail _composedEmail = new(
        Guid.NewGuid(),
        "Test Subject",
        "<p>Test Body</p>",
        "sender@altinnxyz.no",
        "recipient@altinnxyz.no",
        EmailContentType.Html,
        [
            new SasFileReference
            {
                Filename = "file.pdf",
                MimeType = "application/pdf",
                SasUrl = new Uri(_sasUrl)
            }
        ]);

    [Fact]
    public async Task PublishAsync_ValidComposedEmail_MapsAllFieldsAndAttachmentsAndPublishesOnce()
    {
        // Arrange
        SendComposedEmailCommand? capturedCommand = null;
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendComposedEmailCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        var publisher = new ComposedEmailCommandPublisher(wolverinePublisherMock.Object);

        // Act
        await publisher.PublishAsync(_composedEmail, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(_composedEmail.NotificationId, capturedCommand.NotificationId);
        Assert.Equal("Test Subject", capturedCommand.Subject);
        Assert.Equal("<p>Test Body</p>", capturedCommand.Body);
        Assert.Equal("sender@altinnxyz.no", capturedCommand.FromAddress);
        Assert.Equal("recipient@altinnxyz.no", capturedCommand.ToAddress);
        Assert.Equal(EmailContentType.Html.ToString(), capturedCommand.ContentType);

        var attachment = Assert.Single(capturedCommand.Attachments);
        Assert.Equal(_sasUrl, attachment.SasUrl);
        Assert.Equal("file.pdf", attachment.Filename);
        Assert.Equal("application/pdf", attachment.MimeType);

        wolverinePublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOtherException_PropagatesSameException()
    {
        // Arrange
        var thrown = new TimeoutException("Service Bus unavailable");
        var publisher = new ComposedEmailCommandPublisher(CreateThrowingWolverinePublisherMock(thrown).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => publisher.PublishAsync(_composedEmail, TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ForwardsSameTokenToWolverinePublisher()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var wolverinePublisherMock = CreateWolverinePublisherMock();
        var publisher = new ComposedEmailCommandPublisher(wolverinePublisherMock.Object);

        // Act
        await publisher.PublishAsync(_composedEmail, cancellationTokenSource.Token);

        // Assert
        wolverinePublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), cancellationTokenSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_PropagatesSameException()
    {
        // Arrange
        var thrown = new OperationCanceledException();
        var publisher = new ComposedEmailCommandPublisher(CreateThrowingWolverinePublisherMock(thrown).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_composedEmail, TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsContentTooLarge_ThrowsNotificationSizeLimitExceededWithInnerException()
    {
        // Arrange
        var tooLarge = new ContentTooLargeException("too large", new InvalidOperationException("inner"));
        var publisher = new ComposedEmailCommandPublisher(CreateThrowingWolverinePublisherMock(tooLarge).Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotificationSizeLimitExceededException>(
            () => publisher.PublishAsync(_composedEmail, TestContext.Current.CancellationToken));

        Assert.Same(tooLarge, exception.InnerException);
        Assert.Contains(_composedEmail.NotificationId.ToString(), exception.Message);
    }

    private static Mock<IWolverinePublisher> CreateWolverinePublisherMock()
    {
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return wolverinePublisherMock;
    }

    private static Mock<IWolverinePublisher> CreateThrowingWolverinePublisherMock(Exception exception)
    {
        var wolverinePublisherMock = new Mock<IWolverinePublisher>();
        wolverinePublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        return wolverinePublisherMock;
    }
}
