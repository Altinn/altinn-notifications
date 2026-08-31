using Altinn.Notifications.Shared.Publishers;

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

    private sealed record TestItem(Guid Id);

    [Fact]
    public async Task PublishCommandAsync_SuccessfulSend_CompletesWithoutError()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert (no exception)
        await publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        messageBusMock.Verify(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()), Times.Once);
    }

    [Fact]
    public async Task PublishCommandAsync_MessageBusThrows_PropagatesException()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishCommandAsync(new TestCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishCommandAsync_PreCancelledToken_ThrowsOperationCanceledExceptionWithoutSending()
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
    public async Task PublishBatchAsync_EmptyList_ReturnsEmptyListWithoutCallingMessageBus()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishBatchAsync(
            (IReadOnlyList<TestItem>)[],
            item => new TestCommand(item.Id),
            concurrency: 10,
            onError: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task PublishBatchAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        IReadOnlyList<TestItem> items = [new TestItem(Guid.NewGuid())];

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishBatchAsync(items, item => new TestCommand(item.Id), 10, null, cts.Token));

        messageBusMock.Verify(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task PublishBatchAsync_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        IReadOnlyList<TestItem> items = [new TestItem(Guid.NewGuid()), new TestItem(Guid.NewGuid())];

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishBatchAsync(items, item => new TestCommand(item.Id), 10, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PublishBatchAsync_AllFail_ReturnsAllItemsAndInvokesOnErrorForEach()
    {
        // Arrange
        IReadOnlyList<TestItem> items = [new TestItem(Guid.NewGuid()), new TestItem(Guid.NewGuid())];

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        var errors = new List<(TestItem Item, Exception Exception)>();

        // Act
        var result = await publisher.PublishBatchAsync(
            items,
            item => new TestCommand(item.Id),
            10,
            (item, ex) => errors.Add((item, ex)),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.IsType<InvalidOperationException>(e.Exception));
    }

    [Fact]
    public async Task PublishBatchAsync_SomeFail_ReturnsOnlyFailedItems()
    {
        // Arrange
        var successItem = new TestItem(Guid.NewGuid());
        var failItem = new TestItem(Guid.NewGuid());
        IReadOnlyList<TestItem> items = [successItem, failItem];

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<TestCommand>(c => c.Id == successItem.Id), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<TestCommand>(c => c.Id == failItem.Id), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishBatchAsync(items, item => new TestCommand(item.Id), 10, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(failItem.Id, result[0].Id);
    }

    [Fact]
    public async Task PublishBatchAsync_MessageBusThrowsOperationCanceledException_ReturnsFailedItemWithoutThrowing()
    {
        // Arrange
        var item = new TestItem(Guid.NewGuid());

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishBatchAsync(
            [item],
            i => new TestCommand(i.Id),
            10,
            null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(item.Id, result[0].Id);
    }

    private static WolverinePublisher CreatePublisher(Mock<IMessageBus> messageBusMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);

        return new WolverinePublisher(services.BuildServiceProvider());
    }
}
