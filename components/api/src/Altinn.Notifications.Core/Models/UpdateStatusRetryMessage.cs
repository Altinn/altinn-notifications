using System.Text.Json;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Retry envelope for a notification status update that could not be correlated.
/// </summary>
public sealed record UpdateStatusRetryMessage
{
    /// <summary>
    /// Total correlation attempts including the initial failed one.
    /// </summary>
    public required int Attempts { get; init; }

    /// <summary>
    /// UTC timestamp when this retry message was first created.
    /// </summary>
    public required DateTime FirstSeen { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent correlation attempt.
    /// </summary>
    public required DateTime LastAttempt { get; init; }

    /// <summary>
    /// Raw serialized send operation result payload.
    /// </summary>
    public required string SendOperationResult { get; init; }

    /// <summary>
    /// Serializes this instance to JSON.
    /// </summary>
    public string Serialize() => JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
}
