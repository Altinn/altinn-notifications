namespace Altinn.Notifications.Tools.DlqManager.Configuration;

/// <summary>
/// File-path settings for the SMS send queue DLQ operations.
/// </summary>
public class SmsSendQueueSettings
{
    /// <summary>
    /// Output path for DLQ items whose <c>expirytime</c> is in the past.
    /// </summary>
    public string ExpiredListFilePath { get; set; } = "sms-send-dlq-expired.json";

    /// <summary>
    /// Output path for DLQ items whose <c>expirytime</c> is in the future.
    /// </summary>
    public string PendingListFilePath { get; set; } = "sms-send-dlq-pending.json";
}
