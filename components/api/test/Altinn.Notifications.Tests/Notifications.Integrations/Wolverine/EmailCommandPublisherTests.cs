using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Moq;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class EmailCommandPublisherTests
{
    private readonly Email _email = new(
        Guid.NewGuid(),
        "Test Subject",
        "Test Body",
        "sender@altinnxyz.no",
        "recipient@altinnxyz.no",
        EmailContentType.Html);

    [Xunit.Fact]
    public async Task PublishAsync_SuccessfulPublish_PublishesCommand()
    {
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        await publisher.PublishAsync(_email, Xunit.TestContext.Current.CancellationToken);

        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Xunit.Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Xunit.Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(_email, cts.Token));

        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Xunit.Fact]
    public async Task PublishAsync_MessageBusThrowsException_Rethrows()
    {
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        await Xunit.Assert.ThrowsAsync<TimeoutException>(() => publisher.PublishAsync(_email, Xunit.TestContext.Current.CancellationToken));
    }

    [Xunit.Fact]
    public async Task PublishAsync_MapsAllFieldsCorrectlyToSendEmailCommand()
    {
        var notificationId = Guid.NewGuid();
        var email = new Email(notificationId, "Hello", "<p>World</p>", "from@test.no", "to@test.no", EmailContentType.Html);

        SendEmailCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        await publisher.PublishAsync(email, Xunit.TestContext.Current.CancellationToken);

        Xunit.Assert.NotNull(capturedCommand);
        Xunit.Assert.Equal("Hello", capturedCommand!.Subject);
        Xunit.Assert.Equal("<p>World</p>", capturedCommand.Body);
        Xunit.Assert.Equal("to@test.no", capturedCommand.ToAddress);
        Xunit.Assert.Equal("from@test.no", capturedCommand.FromAddress);
        Xunit.Assert.Equal(notificationId, capturedCommand.NotificationId);
        Xunit.Assert.Equal(EmailContentType.Html.ToString(), capturedCommand.ContentType);
    }

    private static EmailCommandPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock)
    {
        return new EmailCommandPublisher(messageBusPublisherMock.Object);
    }
}
