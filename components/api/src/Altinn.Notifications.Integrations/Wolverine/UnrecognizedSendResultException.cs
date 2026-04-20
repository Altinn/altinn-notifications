namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Thrown by send-result handlers when the <c>SendResult</c> value on an incoming command
/// cannot be mapped to a known notification result type.
/// </summary>
public sealed class UnrecognizedSendResultException(string message) : Exception(message);
