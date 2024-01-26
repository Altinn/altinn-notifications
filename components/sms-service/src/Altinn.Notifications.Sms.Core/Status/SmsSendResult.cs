namespace Altinn.Notifications.Sms.Core.Status
{
    /// <summary>
    /// Enum describing sms send result types
    /// </summary>
    public enum SmsSendResult
    {
        /// <summary>
        /// Sms send operation running
        /// </summary>
        Sending,

        /// <summary>
        /// Sms send operation accepted
        /// </summary>
        Accepted,

        /// <summary>
        /// Sms send operation failed
        /// </summary>
        Failed,

        /// <summary>
        /// Sms send operation failed due to invalid receiver
        /// </summary>
        Failed_InvalidReceiver
    }
}
