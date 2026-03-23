using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

/// <summary>
/// Unit tests for <see cref="EmailCommandPublisher"/>.
/// </summary>
public class EmailCommandPublisherTests
{
    private readonly Email _email = new(
        Guid.NewGuid(),
        "Test Subject",
        "Test Body",
        "sender@example.com",
        "recipient@example.com",
        EmailContentType.Html);

    [Fact]
    public async Task PublishAsync_SuccessfulPublish_ReturnsNull()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(_email, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_ReturnsNotificationId()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(_email, CancellationToken.None);

        // Assert
        Assert.Equal(_email.NotificationId, result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_LogsError()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<EmailCommandPublisher>>();
        var publisher = CreatePublisher(messageBusMock, loggerMock);

        // Act
        await publisher.PublishAsync(_email, CancellationToken.None);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, cts.Token));

        messageBusMock.Verify(
            m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_ValidEmail_MapsAllFieldsCorrectlyToSendEmailCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var email = new Email(notificationId, "Hello", "<p>World</p>", "from@test.no", "to@test.no", EmailContentType.Html);

        SendEmailCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishAsync(email, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(notificationId, capturedCommand.NotificationId);
        Assert.Equal("Hello", capturedCommand.Subject);
        Assert.Equal("<p>World</p>", capturedCommand.Body);
        Assert.Equal("from@test.no", capturedCommand.FromAddress);
        Assert.Equal("to@test.no", capturedCommand.ToAddress);
        Assert.Equal(EmailContentType.Html.ToString(), capturedCommand.ContentType);
    }

    [Fact]
    public async Task PublishAsync_PlainContentType_MapsContentTypeEnumToString()
    {
        // Arrange
        var email = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Plain);

        SendEmailCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishAsync(email, CancellationToken.None);

        // Assert
        Assert.Equal("Plain", capturedCommand!.ContentType);
    }

    [Fact]
    public async Task PublishAsync_HtmlContentType_MapsContentTypeEnumToString()
    {
        // Arrange
        var email = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Html);

        SendEmailCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishAsync(email, CancellationToken.None);

        // Assert
        Assert.Equal("Html", capturedCommand!.ContentType);
    }

    private static EmailCommandPublisher CreatePublisher(
        Mock<IMessageBus> messageBusMock,
        Mock<ILogger<EmailCommandPublisher>>? loggerMock = null)
    {
        loggerMock ??= new Mock<ILogger<EmailCommandPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped<IMessageBus>(_ => messageBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        return new EmailCommandPublisher(loggerMock.Object, serviceProvider);
    }
}
