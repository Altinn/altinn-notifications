using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.BackgroundQueue;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.BackgroundQueues;

public class EmailPublishTaskQueueTests
{
    [Fact]
    public void TryEnqueue_ShouldSignalWorkItem_ReturnTrue()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue(NullLogger<EmailPublishTaskQueue>.Instance);

        // Act / Assert
        Assert.True(queue.TryEnqueue());
    }

    [Fact]
    public void TryEnqueue_WhenAlreadyQueued_ReturnFalse()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue(NullLogger<EmailPublishTaskQueue>.Instance);
        queue.TryEnqueue();

        // Act
        var result = queue.TryEnqueue();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task WaitAsync_WhenEnqueued_ShouldCompleteImmediately()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue(NullLogger<EmailPublishTaskQueue>.Instance);
        queue.TryEnqueue();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await queue.WaitAsync(cts.Token);

        // Assert - If we reach here without timeout, test passes
        Assert.True(true);
    }

    [Fact]
    public async Task WaitAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue(NullLogger<EmailPublishTaskQueue>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.WaitAsync(cts.Token));
    }

    [Fact]
    public void MarkCompleted_ThenTryEnqueue_ShouldReturnTrue()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue(NullLogger<EmailPublishTaskQueue>.Instance);
        queue.TryEnqueue();
        queue.MarkCompleted();

        // Act
        var result = queue.TryEnqueue();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task FullWorkflow_EnqueueWaitMarkComplete_ShouldAllowReuse()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue(NullLogger<EmailPublishTaskQueue>.Instance);

        // Act - First cycle
        Assert.True(queue.TryEnqueue());
        await queue.WaitAsync(CancellationToken.None);
        queue.MarkCompleted();

        // Act - Second cycle
        Assert.True(queue.TryEnqueue());
        await queue.WaitAsync(CancellationToken.None);

        // Assert
        Assert.False(queue.TryEnqueue()); // Should be in-flight
    }
}
