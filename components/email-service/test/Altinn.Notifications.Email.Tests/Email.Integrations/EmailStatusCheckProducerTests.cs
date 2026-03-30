using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Producers;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class EmailStatusCheckProducerTests
{
    private static readonly DateTime _fixedTime = new(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
    private const string _topicName = "altinn.notifications.email.sending.accepted";

    [Fact]
    public async Task DispatchAsync_ValidArgs_ProducesSerializedIdentifierToConfiguredTopic()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        const string operationId = "acs-op-abc123";

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(_fixedTime);

        string? capturedTopic = null;
        string? capturedMessage = null;

        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((topic, msg) =>
            {
                capturedTopic = topic;
                capturedMessage = msg;
            })
            .ReturnsAsync(true);

        var sut = new EmailStatusCheckProducer(producerMock.Object, dateTimeMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(notificationId, operationId);

        // Assert
        Assert.Equal(_topicName, capturedTopic);
        Assert.NotNull(capturedMessage);
        Assert.Contains(notificationId.ToString(), capturedMessage);
        Assert.Contains(operationId, capturedMessage);
        Assert.Contains("2025-06-01T10:00:00Z", capturedMessage);
    }

    [Fact]
    public async Task DispatchAsync_AlwaysProducesExactlyOnce()
    {
        // Arrange
        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(_fixedTime);

        var sut = new EmailStatusCheckProducer(producerMock.Object, dateTimeMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(Guid.NewGuid(), "op-id");

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_topicName, It.IsAny<string>()), Times.Once);
    }
}
