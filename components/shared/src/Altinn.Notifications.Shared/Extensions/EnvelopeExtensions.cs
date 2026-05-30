using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Extension methods for <see cref="Envelope"/> to expose custom metadata.
/// </summary>
public static class EnvelopeExtensions
{
    private const string _enqueuedAtKey = "enqueued-at";

    /// <summary>
    /// Exposes the header key used to store the original enqueue time of a message. This is used by the <see cref="EventGridEnvelopeMapper"/>
    /// to preserve the enqueue time across re-enqueues by Wolverine retry policies.
    /// </summary>
    public static string EnqueuedAtHeaderKey { get; set; } = _enqueuedAtKey;

    /// <summary>
    /// Gets the UTC time when the message was originally enqueued, if available.
    /// Falls back to <see cref="Envelope.SentAt"/> if not set.
    /// </summary>
    public static DateTimeOffset EnqueuedAt(this Envelope envelope)
    {
        if (envelope.Headers.TryGetValue(_enqueuedAtKey, out var raw)
            && raw is not null
            && long.TryParse(raw, out var ticks))
        {
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        return envelope.SentAt;
    }

    /// <summary>
    /// Sets the original enqueue time on the envelope headers.
    /// Should only be called once on first receipt.
    /// </summary>
    public static void SetEnqueuedAt(this Envelope envelope, DateTimeOffset enqueuedAt)
    {
        envelope.Headers[_enqueuedAtKey] = enqueuedAt.UtcTicks.ToString();
    }

    /// <summary>
    /// Returns true if the enqueued-at header has already been stamped on this envelope.
    /// </summary>
    public static bool HasEnqueuedAt(this Envelope envelope)
        => envelope.Headers.ContainsKey(_enqueuedAtKey);
}
