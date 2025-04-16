using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Provides a base implementation for tracking deliverable entities in the notification system.
/// </summary>
/// <remarks>
/// This abstract record implements the <see cref="IDeliverableEntityExt"/> interface, offering
/// a standardized foundation for tracking notifications across different delivery channels.
/// 
/// It combines destination addressing with status tracking capabilities, maintaining a consistent
/// implementation pattern for all derived delivery types. The class centralizes common properties
/// including the destination address, current status, detailed description, and timestamp information.
/// 
/// Specialized implementations extend this class to provide channel-specific delivery tracking
/// while inheriting its core tracking functionality.
/// </remarks>
public abstract record DeliverableEntityExt : IDeliverableEntityExt
{
    /// <inheritdoc />
    [JsonPropertyName("destination")]
    public required string Destination { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("status")]
    public required string Status { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("description")]
    public string? StatusDescription { get; init; } = null;

    /// <inheritdoc />
    [JsonPropertyName("lastUpdate")]
    public required DateTime LastUpdate { get; init; } = DateTime.UtcNow;
}
