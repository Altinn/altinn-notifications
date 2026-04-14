using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Producers;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class EmailSendResultProducerTests
{
    private const string _topicName = "altinn.notifications.email.send.result";

    [Fact]
    public async Task DispatchAsync_ProducesExactlyOnce()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailSendResult.Delivered
        };

        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = new EmailSendResultProducer(producerMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(result);

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_topicName, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_PublishesToConfiguredTopic()
    {
        // Arrange
        string? capturedTopic = null;
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailSendResult.Delivered
        };

        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((topic, _) => capturedTopic = topic)
            .ReturnsAsync(true);

        var sut = new EmailSendResultProducer(producerMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.Equal(_topicName, capturedTopic);
    }

    [Fact]
    public async Task DispatchAsync_SerializesResultCorrectly()
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

        string? capturedMessage = null;
        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, msg) => capturedMessage = msg)
            .ReturnsAsync(true);

        var sut = new EmailSendResultProducer(producerMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains(notificationId.ToString(), capturedMessage);
        Assert.Contains(operationId, capturedMessage);
        Assert.Contains("Delivered", capturedMessage);
    }

    [Fact]
    public async Task DispatchAsync_WhenProduceReturnsFalse_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailSendResult.Delivered
        };

        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var sut = new EmailSendResultProducer(producerMock.Object, _topicName);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DispatchAsync(result));
    }

    [Fact]
    public async Task DispatchAsync_WhenOperationIdIsEmpty_SerializedMessageIncludesEmptyOperationId()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = string.Empty,
            SendResult = EmailSendResult.Failed
        };

        string? capturedMessage = null;
        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, msg) => capturedMessage = msg)
            .ReturnsAsync(true);

        var sut = new EmailSendResultProducer(producerMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("\"operationId\":\"\"", capturedMessage);
    }
}
