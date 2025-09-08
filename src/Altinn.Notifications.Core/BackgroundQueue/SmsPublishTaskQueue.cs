using System.Threading.Channels;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Per-policy signaling with duplicate coalescing. One signal per policy wakes exactly one waiting worker for that policy.
/// </summary>
public class SmsPublishTaskQueue : ISmsPublishTaskQueue
{
    private readonly Lock _sync = new();
    private readonly HashSet<SendingTimePolicy> _inFlightOrQueued = [];
    private readonly Dictionary<SendingTimePolicy, Channel<bool>> _channels = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsPublishTaskQueue"/> class.
    /// </summary>
    public SmsPublishTaskQueue()
    {
        foreach (SendingTimePolicy policy in Enum.GetValues<SendingTimePolicy>())
        {
            _channels[policy] = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }
    }

    /// <inheritdoc/>
    public bool TryEnqueue(SendingTimePolicy sendingTimePolicy)
    {
        lock (_sync)
        {
            if (_inFlightOrQueued.Contains(sendingTimePolicy))
            {
                return false;
            }

            _inFlightOrQueued.Add(sendingTimePolicy);

            _channels[sendingTimePolicy].Writer.TryWrite(true);

            return true;
        }
    }

    /// <inheritdoc/>
    public async Task WaitAsync(SendingTimePolicy sendingTimePolicy, CancellationToken cancellationToken)
    {
        _ = await _channels[sendingTimePolicy].Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void MarkCompleted(SendingTimePolicy sendingTimePolicy)
    {
        lock (_sync)
        {
            _inFlightOrQueued.Remove(sendingTimePolicy);
        }
    }
}
