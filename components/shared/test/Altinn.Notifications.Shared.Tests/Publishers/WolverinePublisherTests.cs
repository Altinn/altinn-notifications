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

    [Fact]
    public async Task PublishBatchAsync_CancellationWhileQueuedOnSemaphore_ReturnsQueuedItemAsFailedWithoutThrowing()
    {
        // Arrange
        var runningItem = new TestItem(Guid.NewGuid());
        var queuedItem = new TestItem(Guid.NewGuid());

        using var cts = new CancellationTokenSource();
        var releaseRunningItem = new TaskCompletionSource();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<TestCommand>(c => c.Id == runningItem.Id), It.IsAny<DeliveryOptions?>()))
            .Returns<TestCommand, DeliveryOptions?>((_, _) => new ValueTask(releaseRunningItem.Task));

        var publisher = CreatePublisher(messageBusMock);

        // Act

        // With concurrency capped at 1, runningItem takes the only slot and suspends without
        // releasing it, so queuedItem is deterministically still waiting on the semaphore when
        // cancellation fires.
        var publishTask = publisher.PublishBatchAsync(
            [runningItem, queuedItem],
            i => new TestCommand(i.Id),
            concurrency: 1,
            onError: null,
            cts.Token);

        await cts.CancelAsync();
        releaseRunningItem.SetResult();

        var result = await publishTask;

        // Assert
        Assert.Single(result);
        Assert.Equal(queuedItem.Id, result[0].Id);
        messageBusMock.Verify(
            m => m.SendAsync(It.Is<TestCommand>(c => c.Id == queuedItem.Id), It.IsAny<DeliveryOptions?>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishBatchAsync_RespectsConcurrencyLimit()
    {
        // Arrange
        const int concurrency = 3;
        const int itemCount = 12;

        var lockObj = new object();
        int currentConcurrent = 0;
        int maxObservedConcurrent = 0;

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<TestCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns<TestCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                int current = Interlocked.Increment(ref currentConcurrent);
                lock (lockObj)
                {
                    maxObservedConcurrent = Math.Max(maxObservedConcurrent, current);
                }

                await Task.Delay(30);
                Interlocked.Decrement(ref currentConcurrent);
            })));

        var items = Enumerable.Range(0, itemCount).Select(_ => new TestItem(Guid.NewGuid())).ToList();

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishBatchAsync(items, i => new TestCommand(i.Id), concurrency, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(maxObservedConcurrent > 1, $"Expected concurrent sends but all {itemCount} items were processed sequentially.");
        Assert.True(maxObservedConcurrent <= concurrency, $"Max concurrent sends ({maxObservedConcurrent}) exceeded the configured limit ({concurrency}).");
    }

    private static WolverinePublisher CreatePublisher(Mock<IMessageBus> messageBusMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);

        return new WolverinePublisher(services.BuildServiceProvider());
    }
}
