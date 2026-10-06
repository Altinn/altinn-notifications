using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.BackgroundQueues;

public class EmailPublishTaskQueueTests
{
    private static readonly TimeSpan _shortTimeout = TimeSpan.FromSeconds(2);

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public void TryEnqueue_ShouldSignalWorkItem_ReturnTrue(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(sendingTimePolicy));
    }

    [Fact]
    public void TryEnqueue_DifferentSendingTimePolicies_EnqueuesIndependently()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Anytime));
    }

    [Fact]
    public void TryEnqueue_WhenAlreadyQueuedForPolicy_ReturnsFalse()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        Assert.False(queue.TryEnqueue(SendingTimePolicy.Daytime));
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public async Task WaitAsync_WhenCancelled_ShouldThrowOperationCanceledException(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.WaitAsync(sendingTimePolicy, cts.Token));
    }

    [Fact]
    public async Task WaitAsync_OnlyCompletesForTheSignaledPolicy()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();
        using var cts = new CancellationTokenSource(_shortTimeout);

        var anytimeWait = queue.WaitAsync(SendingTimePolicy.Anytime, cts.Token);
        var daytimeWait = queue.WaitAsync(SendingTimePolicy.Daytime, cts.Token);

        // Act
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));

        // Assert
        await daytimeWait;
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(anytimeWait.IsCompleted);
    }

    [Fact]
    public void TryEnqueue_FirstTimeEnqueued_SecondTimeNotEnqueued_UntilCompleted()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        Assert.False(queue.TryEnqueue(SendingTimePolicy.Daytime));

        queue.MarkCompleted(SendingTimePolicy.Daytime);

        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
    }

    [Fact]
    public void MarkCompleted_OnlyReleasesTheGivenPolicy()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Anytime));

        // Act
        queue.MarkCompleted(SendingTimePolicy.Daytime);

        // Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        Assert.False(queue.TryEnqueue(SendingTimePolicy.Anytime));
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public async Task FullWorkflow_TryEnqueueWaitMarkCompleted_ShouldAllowReuse(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();

        // Act - First cycle
        Assert.True(queue.TryEnqueue(sendingTimePolicy));
        await queue.WaitAsync(sendingTimePolicy, TestContext.Current.CancellationToken);

        // Assert - still in flight until completed
        Assert.False(queue.TryEnqueue(sendingTimePolicy));

        queue.MarkCompleted(sendingTimePolicy);

        // Act - Second cycle
        Assert.True(queue.TryEnqueue(sendingTimePolicy));
        await queue.WaitAsync(sendingTimePolicy, TestContext.Current.CancellationToken);
    }
}
