using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Represents failures when updating the send status of Email or SMS notifications.
/// </summary>
/// <remarks>
/// Initializes a new instance that carries domain-specific failure context for send-status updates.
/// </remarks>
/// <param name="channel">The notification channel the update concerned (Email or Sms)..</param>
/// <param name="identifier">The value of the identifier that was not matched.</param>
/// <param name="identifierType">The type of the identifier that was not matched.</param>
[ExcludeFromCodeCoverage]
public class SendStatusUpdateException(NotificationChannel channel, string identifier, SendStatusIdentifierType identifierType) : Exception(BuildMessage(identifierType, identifier, channel))
{
    /// <summary>
    /// The notification channel the update concerned (Email or Sms).
    /// </summary>
    public NotificationChannel Channel { get; } = channel;

    /// <summary>
    /// The value of the identifier that was not matched.
    /// </summary>
    public string Identifier { get; } = identifier;

    /// <summary>
    /// The type of the identifier that was not matched.
    /// </summary>
    public SendStatusIdentifierType IdentifierType { get; } = identifierType;

    /// <summary>
    /// Builds an error message for notification status update failures.
    /// </summary>
    /// <param name="channel">The notification channel (Email, SMS, etc.).</param>
    /// <param name="identifier">The identifier value that wasn't found.</param>
    /// <param name="identifierType">The type of identifier used in the search.</param>
    /// <returns>A formatted error message.</returns>
    private static string BuildMessage(NotificationChannel channel, string identifier, SendStatusIdentifierType identifierType)
    {
        return $"{channel} status update failed: {identifierType}='{identifier}' not found";
    }
}
