using System.Net;

namespace Altinn.Notifications.Core.Models.ShortMessageService;

/// <summary>
/// Encapsulates the result of a text message delivery attempt through the Altinn Notifications SMS service.
/// </summary>
public record ShortMessageSendResult
{
    /// <summary>
    /// The value indicating whether the message was successfully accepted by the service.
    /// A <c>true</c> value means the message was successfully queued for delivery; <c>false</c> indicates a failure occurred.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The HTTP status code returned by the service endpoint.
    /// </summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>
    /// The error information when the operation fails.
    /// Contains service-provided problem details explaining why the message could not be processed.
    /// </summary>
    public string? ErrorDetails { get; init; }
}
