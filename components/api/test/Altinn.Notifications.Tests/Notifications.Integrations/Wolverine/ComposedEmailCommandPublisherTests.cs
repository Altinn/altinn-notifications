using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Moq;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class ComposedEmailCommandPublisherTests
{
    private static readonly Uri _sasUrl = new("https://storage.example.com/container/file.pdf?sv=2021&sig=abc");

    private readonly ComposedEmail _composedEmail = new(
        Guid.NewGuid(),
        "Test Subject",
        "Test Body",
        "sender@altinnxyz.no",
        "recipient@altinnxyz.no",
        EmailContentType.Html,
        [
            new SasFileReference
            {
                Filename = "file.pdf",
                MimeType = "application/pdf",
                SasUrl = _sasUrl
            }
        ]);

    [Xunit.Fact]
    public async Task PublishAsync_SuccessfulPublish_PublishesCommand()
    {
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        await publisher.PublishAsync(_composedEmail, Xunit.TestContext.Current.CancellationToken);

        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Xunit.Fact]
    public async Task PublishAsync_MessageBusThrowsInvalidOperationException()
    {
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(_composedEmail, Xunit.TestContext.Current.CancellationToken));
    }

    [Xunit.Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Xunit.Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(_composedEmail, cts.Token));

        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Xunit.Fact]
    public async Task PublishAsync_MessageBusThrowsException()
    {
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        await Xunit.Assert.ThrowsAsync<TimeoutException>(() => publisher.PublishAsync(_composedEmail, Xunit.TestContext.Current.CancellationToken));
    }

    [Xunit.Fact]
    public async Task PublishAsync_MapsAllFieldsAndAttachmentsCorrectly()
    {
        var notificationId = Guid.NewGuid();
        var composedEmail = new ComposedEmail(
            notificationId,
            "Hello",
            "<p>Body</p>",
            "from@test.no",
            "to@test.no",
            EmailContentType.Html,
            [
                new SasFileReference
                {
                    Filename = "report.pdf",
                    MimeType = "application/pdf",
                    SasUrl = new Uri("https://blob.example.com/container/report.pdf?sv=2021&sig=abc")
                }
            ]);

        SendComposedEmailCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendComposedEmailCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        await publisher.PublishAsync(composedEmail, Xunit.TestContext.Current.CancellationToken);

        Xunit.Assert.NotNull(capturedCommand);
        Xunit.Assert.Equal("Hello", capturedCommand!.Subject);
        Xunit.Assert.Equal("<p>Body</p>", capturedCommand.Body);
        Xunit.Assert.Equal("to@test.no", capturedCommand.ToAddress);
        Xunit.Assert.Equal("from@test.no", capturedCommand.FromAddress);
        Xunit.Assert.Equal(notificationId, capturedCommand.NotificationId);
        Xunit.Assert.Equal(EmailContentType.Html.ToString(), capturedCommand.ContentType);
        var attachment = Xunit.Assert.Single(capturedCommand.Attachments);
        Xunit.Assert.Equal("report.pdf", attachment.Filename);
        Xunit.Assert.Equal("application/pdf", attachment.MimeType);
        Xunit.Assert.Equal("https://blob.example.com/container/report.pdf?sv=2021&sig=abc", attachment.SasUrl);
    }

    private static ComposedEmailCommandPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock)
    {
        return new ComposedEmailCommandPublisher(messageBusPublisherMock.Object);
    }
}
