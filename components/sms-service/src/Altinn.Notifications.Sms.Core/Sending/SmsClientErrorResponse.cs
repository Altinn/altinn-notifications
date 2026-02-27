using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Core.Sending
{
    /// <summary>
    /// Class representing an error response from an sms client service
    /// </summary>
    public class SmsClientErrorResponse
    {
        /// <summary>
        /// Result for the send operation
        /// </summary>
        public SmsSendResult SendResult { get; set; }

        /// <summary>
        /// The error message from the sms client service
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
