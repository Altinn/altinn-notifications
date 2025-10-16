using System.Threading.Channels;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <inheritdoc/>
public class EmailPublishTaskQueue : IEmailPublishTaskQueue
{
    private readonly Lock _sync = new();
    private bool _inflightOrQueued = false;
    private readonly Channel<bool> _channel = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    /// <inheritdoc/>
    public void MarkCompleted()
    {
        using var scope = _sync.EnterScope();
        _inflightOrQueued = false;
    }

    /// <inheritdoc/>
    public bool TryEnqueue()
    {
        using var scope = _sync.EnterScope();
        if (_inflightOrQueued)
        {
            return false;
        }
        else
        {
            _inflightOrQueued = true;
            _channel.Writer.TryWrite(true);
            return true;
        }
    }

    /// <inheritdoc/>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        _ = await _channel.Reader.ReadAsync(cancellationToken);
    }
}
