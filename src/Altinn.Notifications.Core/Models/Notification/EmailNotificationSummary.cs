namespace Altinn.Notifications.Core.Models.Notification
{
    /// <summary>
    /// An implementation of <see cref="NotificationSummaryBase{TClass}"/> for email notifications"/>
    /// </summary>
    public class EmailNotificationSummary : NotificationSummaryBase<EmailNotificationWithResult>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EmailNotificationSummary"/> class.
        /// </summary>
        public EmailNotificationSummary(Guid orderId) : base(orderId)
        {
        }
    }
}
