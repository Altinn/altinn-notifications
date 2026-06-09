using System.Net;

namespace Altinn.Notifications.Core.Models.InstantEmailService;

/// <summary>
/// Encapsulates the result of an instant email delivery attempt through the Altinn Notifications Email service.
/// </summary>
public record InstantEmailSendResult
{
    /// <summary>
    /// The value indicating whether the email was successfully accepted by the service.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The HTTP status code returned by the service endpoint.
    /// </summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>
    /// The error information when the operation fails.
    /// Contains service-provided problem details explaining why the email could not be processed.
    /// </summary>
    public string? ErrorDetails { get; init; }
}
