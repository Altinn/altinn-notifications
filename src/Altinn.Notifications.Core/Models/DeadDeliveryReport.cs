using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents a report for a delivery that has failed multiple times and is considered dead.
/// </summary>
public record DeadDeliveryReport
{
    /// <summary>
    /// Gets or sets the date and time when the delivery failure was first detected.
    /// </summary>
    public required DateTime FirstSeen { get; init; }

    /// <summary>
    /// Gets or sets the date and time of the last delivery attempt.
    /// </summary>
    public required DateTime LastAttempt { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the delivery issue has been resolved.
    /// </summary>
    public required bool Resolved { get; init; } = false;

    /// <summary>
    /// Gets or sets the total number of delivery attempts made.
    /// </summary>
    public required int AttemptCount { get; init; } = 1;

    /// <summary>
    /// Gets or sets the communication channel source of the delivery report.
    /// </summary>
    public required DeliveryReportChannel Channel { get; init; }

    /// <summary>
    /// Gets or sets the detailed delivery report containing additional information about the failed delivery.
    /// The structure of this object varies based on the <see cref="Channel"/>.
    /// Should be in the format of a JSON string.
    /// </summary>
    public required string DeliveryReport { get; init; }

    /// <summary>
    /// Gets or sets the reason code for why the delivery failed (e.g., "RETRY_THRESHOLD_EXCEEDED", "NOTIFICATION_EXPIRED").
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets or sets a human-readable message describing why the delivery failed.
    /// </summary>
    public string? Message { get; init; }
}
