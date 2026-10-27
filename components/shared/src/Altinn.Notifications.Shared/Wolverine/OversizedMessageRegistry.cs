using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Azure.Messaging.ServiceBus;

namespace Altinn.Notifications.Shared.Wolverine;

/// <summary>
/// Carries message size rejections from Wolverine's sending failure policy back to the publisher that issued the send.
/// Wolverine handles a matching failure inside the sending agent and then returns normally, so the caller
/// would otherwise never learn that the message was rejected. Entries are keyed by the message instance and
/// are released together with it.
/// </summary>
public sealed class OversizedMessageRegistry
{
    private readonly ConditionalWeakTable<object, ServiceBusException> oversizedMessageExceptions = [];

    /// <summary>
    /// Records that Azure Service Bus rejected <paramref name="message"/> because it exceeds the maximum message size.
    /// </summary>
    /// <param name="message">The message instance that was passed to Wolverine.</param>
    /// <param name="exception">The exception raised by Azure Service Bus.</param>
    public void Record(object message, ServiceBusException exception)
    {
        oversizedMessageExceptions.AddOrUpdate(message, exception);
    }

    /// <summary>
    /// Returns and removes the failure recorded for <paramref name="message"/>, if any.
    /// </summary>
    /// <param name="message">The message instance that was passed to Wolverine.</param>
    /// <param name="exception">The recorded exception when the method returns <c>true</c>.</param>
    /// <returns><c>true</c> when a failure was recorded for the message; otherwise <c>false</c>.</returns>
    public bool TryTake(object message, [NotNullWhen(true)] out ServiceBusException? exception)
    {
        if (oversizedMessageExceptions.TryGetValue(message, out exception))
        {
            oversizedMessageExceptions.Remove(message);

            return true;
        }

        return false;
    }
}
