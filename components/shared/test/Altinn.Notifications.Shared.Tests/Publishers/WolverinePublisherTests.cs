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
            () => publisher.PublishBatchAsync(items, item => new TestCommand(item.Id), null, cts.Token));

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
        var result = await publisher.PublishBatchAsync(items, item => new TestCommand(item.Id), null, TestContext.Current.CancellationToken);

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
        var result = await publisher.PublishBatchAsync(items, item => new TestCommand(item.Id), null, TestContext.Current.CancellationToken);

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
            null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(item.Id, result[0].Id);
    }

    [Fact]
    public async Task PublishBatchAsync_CancellationRequestedDuringBatch_SkipsRemainingItemsAndReportsThemAsFailed()
    {
        // Arrange
        var firstItem = new TestItem(Guid.NewGuid());
        var secondItem = new TestItem(Guid.NewGuid());
        IReadOnlyList<TestItem> items = [firstItem, secondItem];

        using var cts = new CancellationTokenSource();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<TestCommand>(c => c.Id == firstItem.Id), It.IsAny<DeliveryOptions?>()))
            .Returns(() =>
            {
                // Cancelling synchronously here—before the second item's admission check
                // runs—removes any timing race: Task.WhenAll(items.Select(...)) dispatches
                // each item's lambda synchronously in order, so by the time item2's check
                // executes, cancellation is guaranteed to already be visible.
                cts.Cancel();
                return ValueTask.CompletedTask;
            });

        var publisher = CreatePublisher(messageBusMock);

        var errors = new List<(TestItem Item, Exception Exception)>();

        // Act
        var result = await publisher.PublishBatchAsync(
            items,
            item => new TestCommand(item.Id),
            (item, ex) => errors.Add((item, ex)),
            cts.Token);

        // Assert: the second item is skipped entirely—SendAsync must never be called for it.
        messageBusMock.Verify(
            m => m.SendAsync(It.Is<TestCommand>(c => c.Id == secondItem.Id), It.IsAny<DeliveryOptions?>()),
            Times.Never);

        Assert.DoesNotContain(firstItem, result);
        Assert.Contains(secondItem, result);
        Assert.Contains(errors, e => e.Item.Id == secondItem.Id && e.Exception is OperationCanceledException);
    }

    private static WolverinePublisher CreatePublisher(Mock<IMessageBus> messageBusMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);

        return new WolverinePublisher(services.BuildServiceProvider());
    }
}
