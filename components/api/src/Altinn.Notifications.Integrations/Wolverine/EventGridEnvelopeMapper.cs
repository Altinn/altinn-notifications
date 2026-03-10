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
    /// <remarks>This method initializes the envelope's message with a new instance of
    /// EmailDeliveryReportCommand based on the incoming message. The envelope's MessageType is set to the fully
    /// qualified name of EmailDeliveryReportCommand.</remarks>
    /// <param name="envelope">The envelope to which the email delivery report command will be assigned. Cannot be null.</param>
    /// <param name="incoming">The incoming service bus message containing the data used to create the email delivery report command. Cannot be
    /// null.</param>
    public void MapIncomingToEnvelope(Envelope envelope, ServiceBusReceivedMessage incoming)
    {
        envelope.Message = new EmailDeliveryReportCommand(incoming);
        envelope.MessageType = typeof(EmailDeliveryReportCommand).FullName;
    }

    /// <summary>
    /// Maps the specified envelope to an outgoing ServiceBusMessage. This method is not supported for outgoing
    /// messages.
    /// </summary>
    /// <remarks>This method is intended for mapping incoming messages and does not support outgoing message
    /// mapping.</remarks>
    /// <param name="envelope">The envelope containing the incoming message data to be mapped.</param>
    /// <param name="outgoing">The ServiceBusMessage instance that represents the outgoing message to be populated.</param>
    /// <exception cref="NotSupportedException">Thrown when this method is called, as only mapping of incoming Event Grid messages is supported.</exception>
    public void MapEnvelopeToOutgoing(Envelope envelope, ServiceBusMessage outgoing)
    {
        throw new NotSupportedException("This mapper only supports incoming Event Grid messages.");
    }

    /// <summary>
    /// List of headers to be included in the envelope when mapping from an incoming message. This implementation does not include any headers.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<string> AllHeaders() => [];
}
