using System.Text.Json.Serialization;

namespace Altinn.Notifications.Tools.DlqManager.Models;

/// <summary>
/// Represents a single message found on the SMS send DLQ, enriched with database state.
/// Serialised to the expired/pending list files that operators inspect and optionally edit.
/// </summary>
public sealed class DlqSmsItem
{
    // ── Original SendSmsCommand fields ─────────────────────────────────────────

    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; }

    [JsonPropertyName("mobileNumber")]
    public string MobileNumber { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("senderNumber")]
    public string SenderNumber { get; init; } = string.Empty;

    // ── Database state at inspection time ──────────────────────────────────────

    /// <summary>Current <c>result</c> column value from <c>notifications.smsnotifications</c>.</summary>
    [JsonPropertyName("currentDbResult")]
    public string? CurrentDbResult { get; init; }

    /// <summary>Value of <c>expirytime</c> from the database row (UTC).</summary>
    [JsonPropertyName("expiryTime")]
    public DateTime? ExpiryTime { get; init; }

    // ── DLQ message metadata ───────────────────────────────────────────────────

    /// <summary>
    /// The <c>ServiceBusReceivedMessage.MessageId</c>. Used to match this list entry
    /// back to the physical DLQ message during processing operations.
    /// </summary>
    [JsonPropertyName("dlqMessageId")]
    public string DlqMessageId { get; init; } = string.Empty;

    /// <summary>When the message was originally enqueued (UTC).</summary>
    [JsonPropertyName("dlqEnqueuedTime")]
    public DateTime DlqEnqueuedTime { get; init; }

    /// <summary>The dead-letter reason set by the broker or Wolverine.</summary>
    [JsonPropertyName("dlqDeadLetterReason")]
    public string? DlqDeadLetterReason { get; init; }

    /// <summary>Extended description of the dead-letter reason.</summary>
    [JsonPropertyName("dlqDeadLetterErrorDescription")]
    public string? DlqDeadLetterErrorDescription { get; init; }
}
