using Altinn.Notifications.Core.Enums;

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
    /// Gets the current lifecycle status of the entity.
    /// </summary>
    /// <value>
    /// A <see cref="ProcessingLifecycle"/> enum value representing the current state
    /// in the notification processing workflow.
    /// </value>
    /// <remarks>
    /// The status follows a standardized set of states defined by the <see cref="ProcessingLifecycle"/> enum,
    /// with distinct values for order processing, SMS notifications, and email notifications.
    /// This allows for consistent filtering, reporting, and processing of entities based on
    /// their current state within the system.
    /// </remarks>
    ProcessingLifecycle Status { get; }

    /// <summary>
    /// Gets the date and time when the status was last updated.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> representing when the status was most recently modified.
    /// </value>
    /// <remarks>
    /// This timestamp facilitates chronological tracking of status changes, providing
    /// an audit trail for the notification process and enabling time-based analytics
    /// on processing efficiency.
    /// </remarks>
    DateTime LastUpdate { get; }
}
