namespace Altinn.Notifications.Sms.Core.Status
{
    /// <summary>
    /// Enum describing sms send result types
    /// </summary>
    public enum SmsSendResult
    {
        Sending,
        Accepted,
        Failed,
        Failed_InvalidReceiver
    }
}
