using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Producers;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class EmailStatusCheckPublisherTests
{
    private static readonly DateTime _fixedTime = new(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DispatchAsync_ValidArgs_SendsCommandWithCorrectFields()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        const string operationId = "AD9B56A3-2C46-4B22-B090-70CB7B104E2E";

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(_fixedTime);

        DeliveryOptions? capturedOptions = null;
        CheckEmailSendStatusCommand? capturedCommand = null;

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(b => b.SendAsync(It.IsAny<CheckEmailSendStatusCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<CheckEmailSendStatusCommand, DeliveryOptions?>((msg, opts) =>
            {
                capturedCommand = msg;
                capturedOptions = opts;
            })
            .Returns(ValueTask.CompletedTask);

        var emailStatusCheckPublisher = new EmailStatusCheckPublisher(CreateServiceProvider(messageBusMock.Object), dateTimeMock.Object);

        // Act
        await emailStatusCheckPublisher.DispatchAsync(notificationId, operationId);

        // Assert
        messageBusMock.Verify(b => b.SendAsync(It.IsAny<CheckEmailSendStatusCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);

        Assert.NotNull(capturedCommand);
        Assert.Equal(operationId, capturedCommand.SendOperationId);
        Assert.Equal(_fixedTime, capturedCommand.LastCheckedAtUtc);
        Assert.Equal(notificationId, capturedCommand.NotificationId);

        Assert.NotNull(capturedOptions);
        Assert.Equal(TimeSpan.FromMilliseconds(8000), capturedOptions.ScheduleDelay);
    }

    [Fact]
    public async Task DispatchAsync_NullOperationId_ThrowsArgumentNullException()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var dateTimeMock = new Mock<IDateTimeService>();
        var emailStatusCheckPublisher = new EmailStatusCheckPublisher(CreateServiceProvider(messageBusMock.Object), dateTimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => emailStatusCheckPublisher.DispatchAsync(Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task DispatchAsync_EmptyNotificationId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var dateTimeMock = new Mock<IDateTimeService>();
        var emailStatusCheckPublisher = new EmailStatusCheckPublisher(CreateServiceProvider(messageBusMock.Object), dateTimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => emailStatusCheckPublisher.DispatchAsync(Guid.Empty, "some-operation-id"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DispatchAsync_WhitespaceOperationId_ThrowsArgumentException(string operationId)
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var dateTimeMock = new Mock<IDateTimeService>();
        var emailStatusCheckPublisher = new EmailStatusCheckPublisher(CreateServiceProvider(messageBusMock.Object), dateTimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => emailStatusCheckPublisher.DispatchAsync(Guid.NewGuid(), operationId));
    }

    private static IServiceProvider CreateServiceProvider(IMessageBus messageBus)
    {
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(sp => sp.GetService(typeof(IMessageBus))).Returns(messageBus);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(s => s.ServiceProvider).Returns(scopeServiceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        var rootServiceProvider = new Mock<IServiceProvider>();
        rootServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        return rootServiceProvider.Object;
    }
}
