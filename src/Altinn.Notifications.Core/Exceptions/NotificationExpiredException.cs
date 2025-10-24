using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to update a notification that has passed its expiry time (TTL).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NotificationExpiredException : SendStatusUpdateException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationExpiredException"/> class.
    /// </summary>
    /// <param name="channel">The notification channel (Email or Sms).</param>
    /// <param name="identifier">The value of the identifier for the expired notification.</param>
    /// <param name="identifierType">The type of identifier used.</param>
    public NotificationExpiredException(
        NotificationChannel channel,
        string identifier,
        SendStatusIdentifierType identifierType)
        : base(channel, identifier, identifierType, BuildMessage(channel, identifier, identifierType))
    {
    }

    private static string BuildMessage(NotificationChannel channel, string identifier, SendStatusIdentifierType identifierType)
    {
        return $"{channel} status update skipped: {identifierType}='{identifier}' has expired (past TTL)";
    }
}
