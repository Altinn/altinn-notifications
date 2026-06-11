namespace Altinn.Notifications.Tools.DlqManager.Configuration;

/// <summary>
/// File-path settings for the SMS send queue DLQ operations.
/// </summary>
public class SmsSendQueueSettings
{
    /// <summary>
    /// Output path for DLQ items with <c>result = 'Sending'</c> and <c>expirytime</c> in the past.
    /// These are processed by marking the DB result to <c>Accepted</c> so the expiry cron
    /// finalises them as <c>Failed_TTL</c>.
    /// </summary>
    public string SendingExpiredListFilePath { get; set; } = "sms-send-dlq-sending-expired.json";

    /// <summary>
    /// Output path for DLQ items with <c>result = 'Sending'</c> and <c>expirytime</c> in the future.
    /// These are processed by resubmitting the command to the main send queue.
    /// </summary>
    public string SendingPendingListFilePath { get; set; } = "sms-send-dlq-sending-pending.json";

    /// <summary>
    /// Output path for DLQ items whose DB result is anything other than <c>Sending</c>.
    /// These should only be purged from the DLQ — no DB update or re-queue is appropriate.
    /// </summary>
    public string OtherStatusListFilePath { get; set; } = "sms-send-dlq-other.json";
}
