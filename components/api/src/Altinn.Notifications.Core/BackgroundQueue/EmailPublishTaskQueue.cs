using System.Threading.Channels;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <inheritdoc/>
public class EmailPublishTaskQueue() : IEmailPublishTaskQueue
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    /// <inheritdoc/>
    public bool TryEnqueue()
    {
        return _channel.Writer.TryWrite(true);
    }

    /// <inheritdoc/>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        _ = await _channel.Reader.ReadAsync(cancellationToken);
    }
}
