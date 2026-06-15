using System.Text.Json.Serialization;

namespace Altinn.Notifications.Tools.DlqManager.Models;

/// <summary>
/// Represents a single message found on the past due orders DLQ, enriched with database state.
/// Serialised to the list files that operators inspect and optionally edit.
/// </summary>
public sealed class DlqPastDueOrderItem
{
    // ── Order identification (from DLQ message) ────────────────────────────────

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    [JsonPropertyName("notificationChannel")]
    public string NotificationChannel { get; init; } = string.Empty;

    /// <summary>Whether the original DLQ message was a retry attempt (<c>IsProcessOrderRetry = true</c>).</summary>
    [JsonPropertyName("isProcessOrderRetry")]
    public bool IsProcessOrderRetry { get; init; }

    // ── Database state at inspection time ──────────────────────────────────────

    /// <summary>Current <c>processedstatus</c> column value from <c>notifications.orders</c>. Null when the order is not found in the DB.</summary>
    [JsonPropertyName("currentDbStatus")]
    public string? CurrentDbStatus { get; init; }

    /// <summary>Total number of SMS + email notifications created for this order at inspection time.</summary>
    [JsonPropertyName("notificationCount")]
    public long NotificationCount { get; init; }

    /// <summary><c>requestedsendtime + 48 h</c> computed by the database — the canonical expiry boundary.</summary>
    [JsonPropertyName("expiryTime")]
    public DateTime? ExpiryTime { get; init; }

    // ── DLQ message metadata ───────────────────────────────────────────────────

    [JsonPropertyName("dlqMessageId")]
    public string DlqMessageId { get; init; } = string.Empty;

    [JsonPropertyName("dlqEnqueuedTime")]
    public DateTime DlqEnqueuedTime { get; init; }

    [JsonPropertyName("dlqDeadLetterReason")]
    public string? DlqDeadLetterReason { get; init; }

    [JsonPropertyName("dlqDeadLetterErrorDescription")]
    public string? DlqDeadLetterErrorDescription { get; init; }
}
