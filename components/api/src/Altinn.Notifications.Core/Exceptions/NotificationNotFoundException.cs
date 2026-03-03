using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Exception thrown when a notification cannot be found by the provided identifier.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NotificationNotFoundException : SendStatusUpdateException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationNotFoundException"/> class.
    /// </summary>
    /// <param name="channel">The notification channel (Email or Sms).</param>
    /// <param name="identifier">The value of the identifier that was not found.</param>
    /// <param name="identifierType">The type of identifier used in the search.</param>
    public NotificationNotFoundException(
        NotificationChannel channel,
        string identifier,
        SendStatusIdentifierType identifierType)
        : base(channel, identifier, identifierType, BuildMessage(channel, identifier, identifierType))
    {
    }

    private static string BuildMessage(NotificationChannel channel, string identifier, SendStatusIdentifierType identifierType)
    {
        return $"{channel} status update failed: {identifierType}='{identifier}' not found";
    }
}
