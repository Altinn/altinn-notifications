namespace Altinn.Notifications.Core.Models.Notification
{
    /// <summary>
    /// An implementation of <see cref="INotificationSummary{TClass}"/> for sms notifications"/>
    /// </summary>
    public class SmsNotificationSummary : INotificationSummary<SmsNotificationWithResult>
    {
        /// <inheritdoc/>  
        public Guid OrderId { get; set; }

        /// <inheritdoc/>  
        public string? SendersReference { get; set; }

        /// <inheritdoc/>  
        public int Generated { get; internal set; }

        /// <inheritdoc/>  
        public int Succeeded { get; internal set; }

        /// <inheritdoc/>  
        public List<SmsNotificationWithResult> Notifications { get; set; } = new List<SmsNotificationWithResult>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SmsNotificationSummary"/> class.
        /// </summary>
        public SmsNotificationSummary(Guid orderId)
        {
            OrderId = orderId;
        }
    }
}
