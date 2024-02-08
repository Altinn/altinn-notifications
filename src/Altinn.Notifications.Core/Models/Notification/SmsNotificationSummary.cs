namespace Altinn.Notifications.Core.Models.Notification
{
    /// <summary>
    /// An implementation of <see cref="NotificationSummaryBase{TClass}"/> for sms notifications"/>
    /// </summary>
    public class SmsNotificationSummary : NotificationSummaryBase<SmsNotificationWithResult>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmsNotificationSummary"/> class.
        /// </summary>
        public SmsNotificationSummary(Guid orderId) : base(orderId)
        {
        }
    }
}
