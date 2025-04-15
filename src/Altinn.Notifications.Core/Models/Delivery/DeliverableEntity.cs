using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.Delivery;

/// <summary>
/// Provides a base implementation for tracking deliverable entities in the notification system.
/// </summary>
/// <remarks>
/// This abstract record implements the <see cref="IDeliverableEntity"/> interface, offering
/// a standardized foundation for tracking notifications across different delivery channels.
/// 
/// It combines destination addressing with status tracking capabilities, maintaining a consistent
/// implementation pattern for all derived delivery types. The class centralizes common properties
/// including the destination address, current status, detailed description, and timestamp information.
/// 
/// Specialized implementations extend this class to provide channel-specific delivery tracking
/// while inheriting its core tracking functionality.
/// </remarks>
public abstract record DeliverableEntity : IDeliverableEntity
{
    /// <inheritdoc />
    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("description")]
    public string? StatusDescription { get; init; } = null;

    /// <inheritdoc />
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; init; } = DateTime.UtcNow;
}
