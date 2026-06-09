using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Exception thrown when a delivery report message is malformed or contains an unrecognized payload,
/// indicating a permanent failure that should not be retried.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidDeliveryReportException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidDeliveryReportException"/> class.
    /// </summary>
    /// <param name="message">A description of the invalid delivery report condition.</param>
    public InvalidDeliveryReportException(string message) : base(message)
    {
    }
}
