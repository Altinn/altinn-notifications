using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.Status;

/// <summary>
/// Represents standardized status information for trackable entities in the notification system.
/// </summary>
/// <remarks>
/// This interface defines a common structure for exposing status, an optional description,
/// and the last update timestamp. It ensures consistent representation of status-related data
/// across different entity types and delivery channels.
/// </remarks>
public interface IStatus
{
    /// <summary>
    /// Gets the current status of the entity.
    /// </summary>
    /// <value>
    /// A string representing the state (e.g., "Registered", "Processed", "Delivered", "Failed").
    /// </value>
    /// <remarks>
    /// The status value follows a standardized set of states across the notification system,
    /// allowing for consistent filtering, reporting, and processing of entities based on
    /// their current state.
    /// </remarks>
    [JsonPropertyName("status")]
    string Status { get; }

    /// <summary>
    /// Gets a detailed description of the current status.
    /// </summary>
    /// <value>
    /// A human-readable explanation of the current status, including additional context or error details when applicable.
    /// </value>
    /// <remarks>
    /// While the <see cref="Status"/> property provides a standardized state identifier,
    /// this description offers more detailed information about the specific circumstances
    /// of the current entity state, particularly useful for troubleshooting or auditing.
    /// </remarks>
    [JsonPropertyName("description")]
    string? StatusDescription { get; }

    /// <summary>
    /// Gets the UTC date and time when the status was last updated.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> in UTC format representing when the status was most recently modified.
    /// </value>
    /// <remarks>
    /// This timestamp facilitates chronological tracking of status changes, providing
    /// an audit trail for the notification process and enabling time-based analytics
    /// on processing efficiency.
    /// </remarks>
    [JsonPropertyName("lastUpdate")]
    DateTime LastUpdate { get; }
}
