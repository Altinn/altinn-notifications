using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Wolverine;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations.Publishers;

/// <summary>
/// Unit tests for <see cref="AsbSmsDeliveryReportPublisher"/>.
/// </summary>
public class AsbSmsDeliveryReportPublisherTests
{
    [Fact]
    public async Task PublishAsync_ValidResult_SendsCommandViaMessageBus()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var result = new SendOperationResult
        {
            NotificationId = notificationId,
            GatewayReference = "gw-ref-123",
            SendResult = SmsSendResult.Accepted
        };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SmsDeliveryReportCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var serviceProvider = BuildServiceProvider(messageBusMock);
        var publisher = new AsbSmsDeliveryReportPublisher(serviceProvider);

        // Act
        await publisher.PublishAsync(result);

        // Assert
        messageBusMock.Verify(
            m => m.SendAsync(
                It.Is<SmsDeliveryReportCommand>(c =>
                    c.NotificationId == notificationId &&
                    c.GatewayReference == "gw-ref-123" &&
                    c.SendResult == "Accepted"),
                It.IsAny<DeliveryOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_NullSendResult_SendsEmptyString()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = "gw-ref-456",
            SendResult = null
        };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SmsDeliveryReportCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var serviceProvider = BuildServiceProvider(messageBusMock);
        var publisher = new AsbSmsDeliveryReportPublisher(serviceProvider);

        // Act
        await publisher.PublishAsync(result);

        // Assert
        messageBusMock.Verify(
            m => m.SendAsync(
                It.Is<SmsDeliveryReportCommand>(c => c.SendResult == string.Empty),
                It.IsAny<DeliveryOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrows_ExceptionPropagates()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = "gw-ref-789",
            SendResult = SmsSendResult.Failed
        };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SmsDeliveryReportCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Bus unavailable"));

        var serviceProvider = BuildServiceProvider(messageBusMock);
        var publisher = new AsbSmsDeliveryReportPublisher(serviceProvider);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(result));
    }

    private static IServiceProvider BuildServiceProvider(Mock<IMessageBus> messageBusMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);
        return services.BuildServiceProvider();
    }
}
