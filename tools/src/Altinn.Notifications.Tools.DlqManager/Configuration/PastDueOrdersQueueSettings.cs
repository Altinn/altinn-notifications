namespace Altinn.Notifications.Tools.DlqManager.Configuration;

/// <summary>
/// File-path settings for the past due orders DLQ operations.
/// Each list file holds a specific category of orders classified during an Inspect run.
/// </summary>
public class PastDueOrdersQueueSettings
{
    /// <summary>
    /// Orders with <c>processedstatus = 'Registered'</c>, 0 notifications, and expiry in the future.
    /// Safe to resubmit — they have never been processed and the send window is still open.
    /// </summary>
    public string RegisteredPendingListFilePath { get; set; } = "orders-pastdue-dlq-registered-pending.json";

    /// <summary>
    /// Orders with <c>processedstatus = 'Registered'</c>, 0 notifications, and expiry in the past.
    /// The send window has closed; DLQ messages should be purged only.
    /// </summary>
    public string RegisteredExpiredListFilePath { get; set; } = "orders-pastdue-dlq-registered-expired.json";

    /// <summary>
    /// Orders with <c>processedstatus = 'Processing'</c>, 0 notifications, and expiry in the future.
    /// Resubmission is risky — a concurrent API instance may still be processing them.
    /// Confirm carefully before resubmitting.
    /// </summary>
    public string ProcessingPendingListFilePath { get; set; } = "orders-pastdue-dlq-processing-pending.json";

    /// <summary>
    /// Orders with <c>processedstatus = 'Processing'</c>, 0 notifications, and expiry in the past.
    /// The send window has closed; DLQ messages should be purged only.
    /// </summary>
    public string ProcessingExpiredListFilePath { get; set; } = "orders-pastdue-dlq-processing-expired.json";

    /// <summary>
    /// Orders that already have one or more notifications in the database (SMS or email).
    /// Do NOT resubmit — notifications were created despite the assumed rollback.
    /// DLQ messages must be purged to avoid creating duplicate notifications.
    /// </summary>
    public string HasNotificationsListFilePath { get; set; } = "orders-pastdue-dlq-has-notifications.json";

    /// <summary>
    /// Orders whose <c>processedstatus</c> is neither <c>Registered</c> nor <c>Processing</c>
    /// (e.g. <c>Completed</c>, <c>Processed</c>, <c>Cancelled</c>, <c>SendConditionNotMet</c>),
    /// or that are not found in the database at all.
    /// DLQ messages should be purged only — no further action is appropriate.
    /// </summary>
    public string OtherStatusListFilePath { get; set; } = "orders-pastdue-dlq-other-status.json";
}
