using System.Threading.Channels;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.BackgroundQueue;

/// <summary>
/// Channel-based queue with duplicate coalescing per sending time policy.
/// </summary>
public class SmsSendBackgroundQueue : ISmsSendBackgroundQueue
{
    private readonly Lock _lock = new();
    private readonly Channel<SendingTimePolicy> _sendingTimePolicyChannel;
    private readonly HashSet<SendingTimePolicy> _queuedSendingTimePolicies = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsSendBackgroundQueue"/> class.
    /// </summary>
    public SmsSendBackgroundQueue()
    {
        _sendingTimePolicyChannel = Channel.CreateUnbounded<SendingTimePolicy>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    /// <inheritdoc/>
    public bool TryEnqueue(SendingTimePolicy sendingTimePolicy)
    {
        bool shouldEnqueue;
        lock (_lock)
        {
            if (_queuedSendingTimePolicies.Contains(sendingTimePolicy))
            {
                return false;
            }

            _queuedSendingTimePolicies.Add(sendingTimePolicy);
            shouldEnqueue = true;
        }

        if (!shouldEnqueue)
        {
            return false;
        }

        if (!_sendingTimePolicyChannel.Writer.TryWrite(sendingTimePolicy))
        {
            lock (_lock)
            {
                _queuedSendingTimePolicies.Remove(sendingTimePolicy);
            }

            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public void MarkCompleted(SendingTimePolicy sendingTimePolicy)
    {
        lock (_lock)
        {
            _queuedSendingTimePolicies.Remove(sendingTimePolicy);
        }
    }

    /// <inheritdoc/>
    public async Task<SendingTimePolicy> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _sendingTimePolicyChannel.Reader.ReadAsync(cancellationToken);
    }
}
