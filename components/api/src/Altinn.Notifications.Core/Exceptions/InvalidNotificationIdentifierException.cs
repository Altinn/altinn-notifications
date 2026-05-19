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
}
