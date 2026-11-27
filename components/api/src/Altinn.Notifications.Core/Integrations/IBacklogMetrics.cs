namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Emits custom backlog metrics for notifications and orders.
/// </summary>
public interface IBacklogMetrics
{
    /// <summary>
    /// Records current backlog values for email notifications, SMS notifications, and orders.
    /// </summary>
    /// <param name="emailNotificationsAnytime">Pending email notifications for anytime policy.</param>
    /// <param name="emailNotificationsDaytime">Pending email notifications for daytime policy.</param>
    /// <param name="emailNotificationsComposed">Pending composed email notifications.</param>
    /// <param name="emailNotificationsSending">Email notifications with Sending status, including composed emails.</param>
    /// <param name="smsNotificationsAnytime">Pending SMS notifications for anytime policy.</param>
    /// <param name="smsNotificationsDaytime">Pending SMS notifications for daytime policy.</param>
    /// <param name="smsNotificationsSending">SMS notifications with Sending status.</param>
    /// <param name="ordersNotDue">Orders not yet due for processing.</param>
    /// <param name="ordersPastDue">Orders past due for processing.</param>
    /// <param name="ordersRetryExhausted">Orders marked with exhausted retries.</param>
    void RecordBacklogMetrics(
        long emailNotificationsAnytime,
        long emailNotificationsDaytime,
        long emailNotificationsComposed,
        long emailNotificationsSending,
        long smsNotificationsAnytime,
        long smsNotificationsDaytime,
        long smsNotificationsSending,
        long ordersNotDue,
        long ordersPastDue,
        long ordersRetryExhausted);
}
