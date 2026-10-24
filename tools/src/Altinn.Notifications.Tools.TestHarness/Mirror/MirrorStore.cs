namespace Altinn.Notifications.Tools.TestHarness.Mirror;

/// <summary>
/// A copy of what a mock received, e.g. an SMS, and the status the mock last reported for it.
/// </summary>
internal sealed class MirroredMessage
{
    public required string Id { get; init; }

    public required string Channel { get; init; }

    /// <summary>
    /// What the mock sent, e.g. a <see cref="MirroredSms"/>. Shown as is, unmasked.
    /// </summary>
    public required object Content { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public string Status { get; set; } = MirrorStatus.Sent;

    /// <summary>
    /// What the mock said, e.g. LinkMobility's <c>UNDELIV</c> behind <see cref="MirrorStatus.Failed"/>.
    /// </summary>
    public string? Detail { get; set; }

    public DateTimeOffset StatusAt { get; set; }
}

/// <summary>
/// The statuses the GUI knows. The mocks map their own to these and put the original in <see cref="MirroredMessage.Detail"/>.
/// </summary>
internal static class MirrorStatus
{
    public const string Sent = "Sent";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
    public const string Expired = "Expired";

    public static readonly string[] All = [Sent, Delivered, Failed, Expired];
}

/// <summary>
/// The last messages of one channel, newest first. It keeps at most <c>capacity</c> and drops the oldest.
/// </summary>
internal sealed class MirrorStore(string channel, int capacity, TimeProvider timeProvider)
{
    private readonly LinkedList<MirroredMessage> _messages = new();

    /// <summary>
    /// Raised when a message arrives, changes status or the channel is cleared, so the GUI can refresh.
    /// </summary>
    public event Action? Changed;

    public string Channel => channel;

    public IReadOnlyList<MirroredMessage> Messages
    {
        get
        {
            lock (_messages)
            {
                return [.. _messages];
            }
        }
    }

    /// <summary>
    /// Adds a message, or replaces the one with the same id, e.g. when a mock retries.
    /// </summary>
    public MirroredMessage Add(string? id, object content, string? status = null, string? detail = null)
    {
        var now = timeProvider.GetUtcNow();
        var message = new MirroredMessage
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id,
            Channel = channel,
            Content = content,
            ReceivedAt = now,
            Status = status ?? MirrorStatus.Sent,
            Detail = detail,
            StatusAt = now,
        };

        lock (_messages)
        {
            var existing = _messages.FirstOrDefault(m => m.Id == message.Id);
            if (existing is not null)
            {
                _messages.Remove(existing);
            }

            _messages.AddFirst(message);
            while (_messages.Count > Math.Max(capacity, 1))
            {
                _messages.RemoveLast();
            }
        }

        Changed?.Invoke();
        return message;
    }

    /// <summary>
    /// Sets the status of a message.
    /// </summary>
    /// <returns><c>false</c> if there is no message with this id, e.g. because it was dropped or the control panel restarted.</returns>
    public bool TryUpdateStatus(string id, string status, string? detail)
    {
        lock (_messages)
        {
            var message = _messages.FirstOrDefault(m => m.Id == id);
            if (message is null)
            {
                return false;
            }

            message.Status = status;
            message.Detail = detail;
            message.StatusAt = timeProvider.GetUtcNow();
        }

        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_messages)
        {
            _messages.Clear();
        }

        Changed?.Invoke();
    }
}

/// <summary>
/// One <see cref="MirrorStore"/> per channel.
/// </summary>
internal sealed class MirrorStores
{
    public const string Sms = "sms";

    private readonly Dictionary<string, MirrorStore> _stores;

    public MirrorStores(int capacity, TimeProvider timeProvider)
    {
        _stores = new[] { Sms }.ToDictionary(c => c, c => new MirrorStore(c, capacity, timeProvider), StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<MirrorStore> All => _stores.Values;

    public MirrorStore this[string channel] => _stores[channel];

    public MirrorStore? Find(string channel) => _stores.TryGetValue(channel, out var store) ? store : null;
}
