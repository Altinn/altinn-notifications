namespace Altinn.Notifications.Core.BackgroundQueue;

/// <inheritdoc/>
public class EmailPublishTaskQueue : IEmailPublishTaskQueue
{
    /// <inheritdoc/>
    public void MarkCompleted()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
