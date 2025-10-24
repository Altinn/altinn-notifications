using System.Threading.Channels;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <inheritdoc/>
public class EmailPublishTaskQueue(ILogger<EmailPublishTaskQueue> logger) : IEmailPublishTaskQueue
{
    private readonly Lock _sync = new();
    private bool _inflightOrQueued = false;
    private readonly ILogger<EmailPublishTaskQueue> _logger = logger;
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
            var writeResult = _channel.Writer.TryWrite(true);
            if (writeResult)
            {
                _inflightOrQueued = true;
                return true;
            }
            else
            {
                _logger.LogError("Failed to write to the email publish task queue channel.");
                _inflightOrQueued = false;
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        _ = await _channel.Reader.ReadAsync(cancellationToken);
    }
}
