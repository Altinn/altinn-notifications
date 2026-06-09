using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Notification
{
    /// <summary>
    /// An implementation of <see cref="INotificationWithResult{TClass, T}"/> for sms notifications"/>
    /// Using the <see cref="SmsRecipient"/> as recipient type and the <see cref="SmsNotificationResultType"/> as result type
    /// </summary>
    public class SmsNotificationWithResult : INotificationWithResult<SmsRecipient, SmsNotificationResultType>
    {
        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/> 
        public bool Succeeded { get; internal set; }

        /// <inheritdoc/> 
        public SmsRecipient Recipient { get; }

        /// <inheritdoc/> 
        public NotificationResult<SmsNotificationResultType> ResultStatus { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmsNotificationWithResult"/> class.
        /// </summary>
        public SmsNotificationWithResult(Guid id, SmsRecipient recipient, NotificationResult<SmsNotificationResultType> result)
        {
            Id = id;
            Recipient = recipient;
            ResultStatus = result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmsNotificationWithResult"/> class.
        /// </summary>
        internal SmsNotificationWithResult(Guid id, bool succeeded, SmsRecipient recipient, NotificationResult<SmsNotificationResultType> result)
        {
            Id = id;
            Succeeded = succeeded;
            Recipient = recipient;
            ResultStatus = result;
        }
    }
}
