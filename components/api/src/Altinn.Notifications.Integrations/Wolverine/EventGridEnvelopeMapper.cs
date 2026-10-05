using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Maps incoming Azure Service Bus messages containing raw Event Grid payloads
/// into Wolverine envelopes with the correct message type.
/// Preserves Wolverine retry state across ScheduleRetry round-trips through the queue.
/// </summary>
[ExcludeFromCodeCoverage]
public class EventGridEnvelopeMapper : IAzureServiceBusEnvelopeMapper
{
    private const string _attemptsKey = "wolverine-attempts";
    private const string _enqueuedAtKey = "wolverine-enqueued-at";
    private const string _diagnosticIdKey = "Diagnostic-Id";
    private const string _traceParentKey = "traceparent";
    private const string _traceStateKey = "tracestate";
    private const string _baggageKey = "baggage";

    /// <summary>
    /// Maps the specified incoming service bus message to the provided envelope by assigning an email delivery report
    /// command. Restores the retry attempt counter and original enqueue time if the message was re-enqueued by a ScheduleRetry policy.
    /// </summary>
    /// <param name="envelope">The envelope to which the email delivery report command will be assigned.</param>
    /// <param name="incoming">The incoming service bus message containing the Event Grid payload.</param>
    public void MapIncomingToEnvelope(Envelope envelope, ServiceBusReceivedMessage incoming)
    {
        envelope.Message = new EmailDeliveryReportCommand(incoming);
        envelope.MessageType = typeof(EmailDeliveryReportCommand).FullName;

        if (incoming.ApplicationProperties.TryGetValue(_attemptsKey, out var attempts) && attempts is int count)
        {
            envelope.Attempts = count;
        }

        if (incoming.ApplicationProperties.TryGetValue(_enqueuedAtKey, out var enqueuedAt) && enqueuedAt is string raw)
        {
            envelope.Headers[EnvelopeExtensions.EnqueuedAtHeaderKey] = raw;
        }
        else
        {
            envelope.SetEnqueuedAt(incoming.EnqueuedTime);
        }

        CopyIncomingTraceHeaderIfPresent(incoming, envelope, _diagnosticIdKey);
        CopyIncomingTraceHeaderIfPresent(incoming, envelope, _traceParentKey);
        CopyIncomingTraceHeaderIfPresent(incoming, envelope, _traceStateKey);
        CopyIncomingTraceHeaderIfPresent(incoming, envelope, _baggageKey);
    }

    /// <summary>
    /// Maps the envelope back to an outgoing ServiceBusMessage by copying the original
    /// Event Grid payload. This is required for Wolverine retry policies
    /// (e.g. <c>ScheduleRetry</c>) that re-enqueue the message.
    /// Preserves the current attempt counter, original enqueue time, and scheduled
    /// delivery time so the retry policy can track progress and delay re-delivery correctly.
    /// </summary>
    /// <param name="envelope">The envelope whose message is an <see cref="EmailDeliveryReportCommand"/>.</param>
    /// <param name="outgoing">The outgoing ServiceBusMessage to populate.</param>
    public void MapEnvelopeToOutgoing(Envelope envelope, ServiceBusMessage outgoing)
    {
        if (envelope.Message is not EmailDeliveryReportCommand command)
        {
            throw new InvalidOperationException(
                $"Expected envelope message of type {nameof(EmailDeliveryReportCommand)}, " +
                $"but received {envelope.Message?.GetType().Name ?? "null"}.");
        }

        outgoing.Body = command.Message.Body;
        outgoing.ContentType = command.Message.ContentType;
        outgoing.Subject = command.Message.Subject;
        outgoing.ApplicationProperties[_attemptsKey] = envelope.Attempts;

        if (envelope.ScheduledTime.HasValue)
        {
            outgoing.ScheduledEnqueueTime = envelope.ScheduledTime.Value.UtcDateTime;
        }

        if (envelope.HasEnqueuedAt())
        {
            outgoing.ApplicationProperties[_enqueuedAtKey] = envelope.Headers[EnvelopeExtensions.EnqueuedAtHeaderKey];
        }

        CopyOutgoingTraceHeaderIfPresent(envelope, outgoing, _diagnosticIdKey);
        CopyOutgoingTraceHeaderIfPresent(envelope, outgoing, _traceParentKey);
        CopyOutgoingTraceHeaderIfPresent(envelope, outgoing, _traceStateKey);
        CopyOutgoingTraceHeaderIfPresent(envelope, outgoing, _baggageKey);
    }

    private static void CopyIncomingTraceHeaderIfPresent(ServiceBusReceivedMessage incoming, Envelope envelope, string key)
    {
        if (incoming.ApplicationProperties.TryGetValue(key, out var value) && value is string stringValue)
        {
            envelope.Headers[key] = stringValue;
        }
    }

    private static void CopyOutgoingTraceHeaderIfPresent(Envelope envelope, ServiceBusMessage outgoing, string key)
    {
        if (envelope.Headers.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            outgoing.ApplicationProperties[key] = value;
        }
    }
}
