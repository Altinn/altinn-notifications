using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Publishers;
using Altinn.Notifications.Shared.Wolverine;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Publishers;

/// <summary>
/// Unit tests for <see cref="WolverinePublisher"/>.
/// </summary>
public class WolverinePublisherTests
{
    private sealed record TestCommand(Guid Id);

    [Fact]
    public async Task PublishCommandAsync_NoFailureRecorded_SendsOnceAndCompletes()
    {
        // Arrange
        var messageBusMock = CreateMessageBus();
        var publisher = CreatePublisher(messageBusMock, new OversizedMessageRegistry());

        // Act
        await publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
    }

    [Fact]
    public async Task PublishCommandAsync_RegistryNotRegistered_SendsOnceAndCompletes()
    {
        // Arrange
        var messageBusMock = CreateMessageBus();
        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
    }

    [Fact]
    public async Task PublishCommandAsync_MessageBusThrows_PropagatesSameException()
    {
        // Arrange
        var thrown = new ServiceBusException("busy", ServiceBusFailureReason.ServiceBusy);
        var publisher = CreatePublisher(CreateThrowingMessageBus(thrown), new OversizedMessageRegistry());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ServiceBusException>(
            () => publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));

        Assert.Same(thrown, exception);
    }

    [Fact]
    public async Task PublishCommandAsync_CancellationRequestedBeforeSend_ThrowsOperationCanceledException()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), cts.Token));

        messageBusMock.Verify(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task PublishCommandAsync_WithCancellationToken_MessageSizeExceededRecorded_ThrowsContentTooLargeException()
    {
        // Arrange
        var publisher = CreatePublisherRecordingFailure();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ContentTooLargeException>(
            () => publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));

        var inner = Assert.IsType<ServiceBusException>(exception.InnerException);
        Assert.Equal(ServiceBusFailureReason.MessageSizeExceeded, inner.Reason);
    }

    [Fact]
    public async Task PublishCommandAsync_WithoutCancellationToken_MessageSizeExceededRecorded_ThrowsContentTooLargeException()
    {
        // Arrange
        var publisher = CreatePublisherRecordingFailure();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ContentTooLargeException>(
            () => publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid())));

        var inner = Assert.IsType<ServiceBusException>(exception.InnerException);
        Assert.Equal(ServiceBusFailureReason.MessageSizeExceeded, inner.Reason);
    }

    [Fact]
    public async Task PublishCommandAsync_WithDeliveryOptions_MessageSizeExceededRecorded_ThrowsContentTooLargeException()
    {
        // Arrange
        var publisher = CreatePublisherRecordingFailure();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ContentTooLargeException>(
            () => publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), new DeliveryOptions()));

        var inner = Assert.IsType<ServiceBusException>(exception.InnerException);
        Assert.Equal(ServiceBusFailureReason.MessageSizeExceeded, inner.Reason);
    }

    [Fact]
    public async Task PublishCommandAsync_FailureAlreadyTaken_IsNotReportedAgain()
    {
        // Arrange
        var registry = new OversizedMessageRegistry();
        var command = new TestCommand(Guid.NewGuid());
        registry.Record(command, new ServiceBusException("too large", ServiceBusFailureReason.MessageSizeExceeded));

        var publisher = CreatePublisher(CreateMessageBus(), registry);

        // Act & Assert
        await Assert.ThrowsAsync<ContentTooLargeException>(
            () => publisher.PublishCommandAsync(command, TestContext.Current.CancellationToken));

        await publisher.PublishCommandAsync(command, TestContext.Current.CancellationToken);
    }

    private static WolverinePublisher CreatePublisherRecordingFailure()
    {
        var registry = new OversizedMessageRegistry();

        return CreatePublisher(CreateMessageBusRecordingFailure(registry), registry);
    }

    private static Mock<IMessageBus> CreateMessageBus()
    {
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        return messageBusMock;
    }

    private static Mock<IMessageBus> CreateThrowingMessageBus(Exception exception)
    {
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(exception);

        return messageBusMock;
    }

    private static Mock<IMessageBus> CreateMessageBusRecordingFailure(OversizedMessageRegistry registry)
    {
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns((TestCommand c, DeliveryOptions? _) =>
            {
                registry.Record(c, new ServiceBusException("too large", ServiceBusFailureReason.MessageSizeExceeded));
                return ValueTask.CompletedTask;
            });

        return messageBusMock;
    }

    private static WolverinePublisher CreatePublisher(Mock<IMessageBus> messageBusMock, OversizedMessageRegistry? registry = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);

        if (registry is not null)
        {
            services.AddSingleton(registry);
        }

        return new WolverinePublisher(services.BuildServiceProvider());
    }
}
