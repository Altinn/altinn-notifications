using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a rate-limit command issued when the email service receives an HTTP 429 response from the Azure Communication Services.
/// </summary>
public sealed record EmailServiceRateLimitCommand
{
    /// <summary>
    /// Serialized JSON payload for the rate-limit event.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the service that produced the command.
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;
}
