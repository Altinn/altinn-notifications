using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.BackgroundQueue;

public class SmsPublishTaskQueueTests
{
    private static readonly TimeSpan _shortTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public void TryEnqueue_DifferentSendingTimePolicies_AreIndependent()
    {
        // Arrange
        var queue = new SmsPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Anytime));
    }

    [Fact]
    public void TryEnqueue_FirstTimeEnqueued_SecondNotEnqueued_UntilCompleted()
    {
        // Arrange
        var queue = new SmsPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        Assert.False(queue.TryEnqueue(SendingTimePolicy.Daytime));

        queue.MarkCompleted(SendingTimePolicy.Daytime);

        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
    }

    [Fact]
    public async Task WaitAsync_CompletesAfterTryEnqueue_WhenWaitingFirst()
    {
        // Arrange
        var queue = new SmsPublishTaskQueue();
        using var cancellationTokenSource = new CancellationTokenSource(_shortTimeout);

        // Act
        var waitTask = queue.WaitAsync(SendingTimePolicy.Daytime, cancellationTokenSource.Token);

        // Small delay to ensure waiter is registered
        await Task.Delay(50);

        var enqueued = queue.TryEnqueue(SendingTimePolicy.Daytime);

        // Assert
        Assert.True(enqueued);

        await waitTask;  // should complete
    }

    [Fact]
    public async Task WaitAsync_CompletesImmediately_WhenEnqueuedBefore()
    {
        // Arrange
        var queue = new SmsPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Anytime));

        // Since a signal is already in the channel, WaitAsync should complete quickly
        var stopwatch = Stopwatch.StartNew();
        await queue.WaitAsync(SendingTimePolicy.Anytime, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task MarkCompleted_AllowsNewEnqueue()
    {
        // Arrange
        var queue = new SmsPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        queue.MarkCompleted(SendingTimePolicy.Daytime);
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));

        // Drain (avoid affecting other tests)
        await queue.WaitAsync(SendingTimePolicy.Daytime, CancellationToken.None);
    }

    [Fact]
    public async Task ParallelTryEnqueue_OnlyOneSucceeds_ForSamePolicy()
    {
        // Arrange
        int success = 0;
        var queue = new SmsPublishTaskQueue();

        // Act
        await Parallel.ForEachAsync(Enumerable.Range(0, 25), async (i, _) =>
        {
            if (queue.TryEnqueue(SendingTimePolicy.Daytime))
            {
                Interlocked.Increment(ref success);
            }

            await Task.Yield();
        });

        // Assert
        Assert.Equal(1, success);
    }

    [Fact]
    public async Task WaitAsync_SecondWaitBlocksWithoutSecondEnqueue()
    {
        // Arrange
        var queue = new SmsPublishTaskQueue();

        // Act / Assert
        // First enqueue + wait consumes the signal
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Daytime));
        await queue.WaitAsync(SendingTimePolicy.Daytime, CancellationToken.None);

        // Second wait should block; use cancellation to assert blocking
        using var cancellationTokenSource = new CancellationTokenSource(150);
        await Assert.ThrowsAsync<OperationCanceledException>(() => queue.WaitAsync(SendingTimePolicy.Daytime, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task TryEnqueue_FalseWhenAlreadyInFlight_TrueAfterMarkCompletedAndConsumed()
    {
        // Arrange
        var queue = new SmsPublishTaskQueue();

        // Act / Assert
        // Enqueue and consume (still marked in-flight until MarkCompleted)
        Assert.True(queue.TryEnqueue(SendingTimePolicy.Anytime));
        await queue.WaitAsync(SendingTimePolicy.Anytime, CancellationToken.None);

        // Still in-flight because MarkCompleted not called
        Assert.False(queue.TryEnqueue(SendingTimePolicy.Anytime));

        queue.MarkCompleted(SendingTimePolicy.Anytime);

        Assert.True(queue.TryEnqueue(SendingTimePolicy.Anytime));

        // Clean up by consuming
        await queue.WaitAsync(SendingTimePolicy.Anytime, CancellationToken.None);
    }
}
