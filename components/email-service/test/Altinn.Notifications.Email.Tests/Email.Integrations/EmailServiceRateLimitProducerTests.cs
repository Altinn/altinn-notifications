using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Producers;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class EmailServiceRateLimitProducerTests
{
    private const string _topicName = "altinn.platform.service.updated";

    private static GenericServiceUpdate ValidUpdate() => new()
    {
        Source = "platform-notifications-email",
        Schema = AltinnServiceUpdateSchema.ResourceLimitExceeded,
        Data = """{"resource":"azure-communication-services-email","resetTime":"2026-01-01T00:00:00Z"}"""
    };

    [Fact]
    public async Task DispatchAsync_ProducesExactlyOnce()
    {
        // Arrange
        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = new EmailServiceRateLimitProducer(producerMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(ValidUpdate());

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_topicName, It.IsAny<string>()), Times.Once);
        producerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DispatchAsync_SerializesUpdateCorrectly()
    {
        // Arrange
        var update = ValidUpdate();
        string? capturedMessage = null;

        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, msg) => capturedMessage = msg)
            .ReturnsAsync(true);

        var sut = new EmailServiceRateLimitProducer(producerMock.Object, _topicName);

        // Act
        await sut.DispatchAsync(update);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("platform-notifications-email", capturedMessage);
        Assert.Contains("ResourceLimitExceeded", capturedMessage);
        Assert.Contains("azure-communication-services-email", capturedMessage);
    }

    [Fact]
    public async Task DispatchAsync_WhenNullUpdate_ThrowsArgumentNullException()
    {
        var sut = new EmailServiceRateLimitProducer(new Mock<ICommonProducer>().Object, _topicName);
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DispatchAsync(null!));
    }

    [Fact]
    public async Task DispatchAsync_WhenProduceReturnsFalse_ThrowsInvalidOperationException()
    {
        // Arrange
        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var sut = new EmailServiceRateLimitProducer(producerMock.Object, _topicName);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DispatchAsync(ValidUpdate()));
    }
}
