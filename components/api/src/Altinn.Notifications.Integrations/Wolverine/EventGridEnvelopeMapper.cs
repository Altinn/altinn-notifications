using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Maps incoming Azure Service Bus messages containing raw Event Grid payloads
/// into Wolverine envelopes with the correct message type.
/// </summary>
[ExcludeFromCodeCoverage]
public class EventGridEnvelopeMapper : IAzureServiceBusEnvelopeMapper
{
    /// <summary>
    /// Maps the specified incoming service bus message to the provided envelope by assigning an email delivery report
    /// command.
    /// </summary>
    /// <param name="envelope">The envelope to which the email delivery report command will be assigned.</param>
    /// <param name="incoming">The incoming service bus message containing the Event Grid payload.</param>
    public void MapIncomingToEnvelope(Envelope envelope, ServiceBusReceivedMessage incoming)
    {
        envelope.Message = new EmailDeliveryReportCommand(incoming);
        envelope.MessageType = typeof(EmailDeliveryReportCommand).FullName;
    }

    /// <summary>
    /// Maps the envelope back to an outgoing ServiceBusMessage by copying the original
    /// Event Grid payload. This is required for Wolverine retry policies
    /// (e.g. <c>ScheduleRetry</c>) that re-enqueue the message.
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
    }
}
