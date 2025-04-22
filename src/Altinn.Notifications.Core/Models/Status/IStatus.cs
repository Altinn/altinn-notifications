namespace Altinn.Notifications.Core.Models.Status;

/// <summary>
/// Represents standardized status information for trackable entities in the notification system.
/// </summary>
/// <remarks>
/// This interface defines a common structure for exposing status and the last update timestamp.
/// It ensures consistent representation of status-related data across different entity types and delivery channels.
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
    string Status { get; }

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
    DateTime LastUpdate { get; }
}
