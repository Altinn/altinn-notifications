using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Publishers;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class EmailServiceRateLimitPublisherTests
{
    private static GenericServiceUpdate ValidUpdate() => new()
    {
        Source = "platform-notifications-email",
        Schema = AltinnServiceUpdateSchema.ResourceLimitExceeded,
        Data = """{"resource":"azure-communication-services-email","resetTime":"2026-01-01T00:00:00Z"}"""
    };

    [Fact]
    public async Task DispatchAsync_WhenNullUpdate_ThrowsArgumentNullException()
    {
        var messageBusMock = new Mock<IMessageBus>();
        var sut = new EmailServiceRateLimitPublisher(CreateServiceProvider(messageBusMock.Object));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DispatchAsync(null!));
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<EmailServiceRateLimitCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_SendsCommandWithCorrectFields()
    {
        // Arrange
        var update = ValidUpdate();
        EmailServiceRateLimitCommand? capturedCommand = null;

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<EmailServiceRateLimitCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<EmailServiceRateLimitCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var sut = new EmailServiceRateLimitPublisher(CreateServiceProvider(messageBusMock.Object));

        // Act
        await sut.DispatchAsync(update);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(update.Source, capturedCommand.Source);
        Assert.Equal(update.Data, capturedCommand.Data);
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<EmailServiceRateLimitCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
    }

    private static ServiceProvider CreateServiceProvider(IMessageBus messageBus)
    {
        var services = new ServiceCollection();
        services.AddSingleton(messageBus);
        return services.BuildServiceProvider();
    }
}
