using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Exception thrown when a send result value received in a command cannot be parsed into a known enum value.
/// This is a non-retriable data error and should result in a dead delivery report.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UnrecognizedSendResultException : Exception
{
    /// <summary>
    /// The raw send result value that could not be parsed.
    /// </summary>
    public string SendResult { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="UnrecognizedSendResultException"/>.
    /// </summary>
    /// <param name="sendResult">The unrecognized send result value.</param>
    public UnrecognizedSendResultException(string sendResult)
        : base($"Unrecognized SendResult value: '{sendResult}'")
    {
        SendResult = sendResult;
    }
}
