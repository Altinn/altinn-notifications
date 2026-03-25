using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Unit tests for <see cref="SmsCommandPublisher"/>, verifying correct mapping,
/// message bus delegation, and error handling behavior.
/// </summary>
public class SmsCommandPublisherTests
{
    private readonly Sms _sms = new(Guid.NewGuid(), "Altinn", "+4799999999", "Test message");

    [Fact]
    public async Task PublishAsync_WhenMessageBusSucceeds_ReturnsNull()
    {
        // Arrange
        var (publisher, _) = CreatePublisher(throwOnSend: false);

        // Act
        var result = await publisher.PublishAsync(_sms, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_WhenMessageBusSucceeds_MapsAllFieldsToCommand()
    {
        // Arrange
        SendSmsCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<SendSmsCommand>(), null))
            .Callback<SendSmsCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var (publisher, _) = CreatePublisher(messageBusMock: messageBusMock);

        // Act
        await publisher.PublishAsync(_sms, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(_sms.NotificationId, capturedCommand.NotificationId);
        Assert.Equal(_sms.Recipient, capturedCommand.MobileNumber);
        Assert.Equal(_sms.Message, capturedCommand.Body);
        Assert.Equal(_sms.Sender, capturedCommand.SenderNumber);
    }

    [Fact]
    public async Task PublishAsync_WhenMessageBusThrows_ReturnsNotificationId()
    {
        // Arrange
        var (publisher, _) = CreatePublisher(throwOnSend: true);

        // Act
        var result = await publisher.PublishAsync(_sms, CancellationToken.None);

        // Assert
        Assert.Equal(_sms.NotificationId, result);
    }

    [Fact]
    public async Task PublishAsync_WhenMessageBusThrows_LogsError()
    {
        // Arrange
        var (publisher, loggerMock) = CreatePublisher(throwOnSend: true);

        // Act
        await publisher.PublishAsync(_sms, CancellationToken.None);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(_sms.NotificationId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private (SmsCommandPublisher Publisher, Mock<ILogger<SmsCommandPublisher>> LoggerMock) CreatePublisher(
        bool throwOnSend = false,
        Mock<IMessageBus>? messageBusMock = null)
    {
        messageBusMock ??= new Mock<IMessageBus>();

        if (throwOnSend)
        {
            messageBusMock
                .Setup(b => b.SendAsync(It.IsAny<SendSmsCommand>(), null))
                .ThrowsAsync(new InvalidOperationException("Bus unavailable"));
        }
        else
        {
            messageBusMock
                .Setup(b => b.SendAsync(It.IsAny<SendSmsCommand>(), null))
                .Returns(ValueTask.CompletedTask);
        }

        var scopedProviderMock = new Mock<IServiceProvider>();
        scopedProviderMock
            .Setup(sp => sp.GetService(typeof(IMessageBus)))
            .Returns(messageBusMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(scopedProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var rootProviderMock = new Mock<IServiceProvider>();
        rootProviderMock
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(scopeFactoryMock.Object);

        var loggerMock = new Mock<ILogger<SmsCommandPublisher>>();
        var publisher = new SmsCommandPublisher(loggerMock.Object, rootProviderMock.Object);

        return (publisher, loggerMock);
    }
}
