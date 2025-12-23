using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.BackgroundQueue;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.BackgroundQueues;

public class EmailPublishTaskQueueTests
{
    [Fact]
    public void TryEnqueue_ShouldSignalWorkItem_ReturnTrue()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();

        // Act / Assert
        Assert.True(queue.TryEnqueue());
    }

    [Fact]
    public async Task WaitAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task FullWorkflow_TryEnqueueWait_ShouldAllowReuse()
    {
        // Arrange
        var queue = new EmailPublishTaskQueue();

        // Act - First cycle
        Assert.True(queue.TryEnqueue());
        await queue.WaitAsync(CancellationToken.None);

        // Act - Second cycle
        Assert.True(queue.TryEnqueue());

        // Assert
        Assert.False(queue.TryEnqueue()); // Should be in-flight
    }
}
