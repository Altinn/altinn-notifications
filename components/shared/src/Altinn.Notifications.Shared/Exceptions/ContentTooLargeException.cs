using Azure.Messaging.ServiceBus;

namespace Altinn.Notifications.Shared.Exceptions;

/// <summary>
/// Thrown when Azure Service Bus rejects a message because it exceeds the maximum message size of the target queue.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ContentTooLargeException"/> class.
/// </remarks>
/// <param name="message">The message that describes the error.</param>
/// <param name="innerException">The <see cref="ServiceBusException"/> raised by Azure Service Bus.</param>
public class ContentTooLargeException(string message, Exception innerException) : Exception(message, innerException)
{
}
