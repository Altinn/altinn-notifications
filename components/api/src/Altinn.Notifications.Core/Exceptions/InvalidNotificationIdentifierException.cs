using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Exception thrown when a notification status update is attempted with no valid identifier
/// (neither a notification ID nor a secondary identifier such as operationId or gatewayReference).
/// This is a non-retriable data error and should result in a dead delivery report.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidNotificationIdentifierException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="InvalidNotificationIdentifierException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidNotificationIdentifierException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InvalidNotificationIdentifierException"/> with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public InvalidNotificationIdentifierException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
