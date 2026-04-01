using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Producers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class EmailStatusCheckPublisherTests
{
    private static readonly DateTime _fixedTime = new(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static IServiceProvider CreateServiceProvider(IMessageBus messageBus)
    {
        var innerSp = new Mock<IServiceProvider>();
        innerSp.Setup(sp => sp.GetService(typeof(IMessageBus))).Returns(messageBus);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(innerSp.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        return serviceProvider.Object;
    }

    [Fact]
    public async Task DispatchAsync_ValidArgs_SendsCheckEmailSendStatusCommandViaBus()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        const string operationId = "acs-op-xyz789";

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(_fixedTime);

        var loggerMock = new Mock<ILogger<EmailStatusCheckPublisher>>();

        CheckEmailSendStatusCommand? capturedCommand = null;

        var busMock = new Mock<IMessageBus>();
        busMock
            .Setup(b => b.PublishAsync(It.IsAny<object>(), It.IsAny<DeliveryOptions?>()))
            .Callback<object, DeliveryOptions?>((msg, _) => capturedCommand = msg as CheckEmailSendStatusCommand)
            .Returns(ValueTask.CompletedTask);

        var sut = new EmailStatusCheckPublisher(CreateServiceProvider(busMock.Object), dateTimeMock.Object, loggerMock.Object);

        // Act
        await sut.DispatchAsync(notificationId, operationId);

        // Assert
        busMock.Verify(b => b.PublishAsync(It.IsAny<CheckEmailSendStatusCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
        Assert.NotNull(capturedCommand);
        Assert.Equal(notificationId, capturedCommand!.NotificationId);
        Assert.Equal(operationId, capturedCommand.SendOperationId);
        Assert.Equal(_fixedTime, capturedCommand.LastCheckedAtUtc);
    }

    [Fact]
    public async Task DispatchAsync_AlwaysSendsExactlyOneCommand()
    {
        // Arrange
        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(_fixedTime);

        var busMock = new Mock<IMessageBus>();
        busMock
            .Setup(b => b.PublishAsync(It.IsAny<object>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var loggerMock = new Mock<ILogger<EmailStatusCheckPublisher>>();

        var sut = new EmailStatusCheckPublisher(CreateServiceProvider(busMock.Object), dateTimeMock.Object, loggerMock.Object);

        // Act
        await sut.DispatchAsync(Guid.NewGuid(), "op-id");

        // Assert
        busMock.Verify(b => b.PublishAsync(It.IsAny<CheckEmailSendStatusCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
    }
}
