using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Exception thrown when a notification's content is larger than the message queue accepts, so the notification can never be queued for sending.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NotificationSizeLimitExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationSizeLimitExceededException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public NotificationSizeLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
