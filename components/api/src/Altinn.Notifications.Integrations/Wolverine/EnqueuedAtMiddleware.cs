using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Wolverine middleware that stamps the <c>enqueued-at</c> header on the envelope
/// the first time a message is received. Subsequent attempts (retries) are no-ops
/// because the header is already present and survives re-enqueue via envelope serialization.
/// </summary>
internal static class EnqueuedAtMiddleware
{
    /// <summary>
    /// Called by Wolverine before the handler executes on each attempt.
    /// Sets <see cref="EnvelopeExtensions.EnqueuedAtHeaderKey"/> from <see cref="Envelope.SentAt"/>
    /// if not already stamped (e.g. by <see cref="EventGridEnvelopeMapper"/> on first receipt).
    /// </summary>
    public static void Before(Envelope envelope)
    {
        if (!envelope.HasEnqueuedAt())
        {
            envelope.SetEnqueuedAt(envelope.SentAt);
        }
    }
}
