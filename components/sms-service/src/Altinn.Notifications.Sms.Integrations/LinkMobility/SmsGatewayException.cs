namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Thrown when a transient failure occurs while communicating with the SMS gateway (e.g. a timeout before the gateway responds).
/// Caught by the gateway error chain in <see cref="Wolverine.Policies.SendSmsCommandHandlerPolicy"/>.
/// </summary>
/// <inheritdoc cref="Exception(string, Exception)"/>
public sealed class SmsGatewayException(string message, Exception innerException) : Exception(message, innerException)
{
}
