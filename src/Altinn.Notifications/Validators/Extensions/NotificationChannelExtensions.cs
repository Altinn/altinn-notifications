using Altinn.Notifications.Models;

namespace Altinn.Notifications.Validators.Extensions
{
    /// <summary>
    /// Extension class for <see cref="NotificationChannelExt"/>
    /// </summary>
    public static class NotificationChannelExtensions
    {
        /// <summary>
        /// Determines whether the specified notification channel is set to send via both email and SMS.
        /// </summary>
        /// <param name="notificationChannelExt">The notification channel to check.</param>
        /// <returns><c>true</c> if the notification channel is set to <see cref="NotificationChannelExt.EmailAndSms"/>; otherwise, <c>false</c>.</returns>
        public static bool IsDualChannelSchema(this NotificationChannelExt notificationChannelExt)
        {
            return notificationChannelExt == NotificationChannelExt.EmailAndSms;
        }

        /// <summary>
        /// Determines whether the specified notification channel is set to send via either email or SMS.
        /// </summary>
        /// <param name="notificationChannelExt">The notification channel to check.</param>
        /// <returns><c>true</c> if the notification channel is set to <see cref="NotificationChannelExt.EmailPreferred"/> or <see cref="NotificationChannelExt.SmsPreferred"/>; otherwise, <c>false</c>.</returns>
        public static bool IsFallbackChannelSchema(this NotificationChannelExt notificationChannelExt)
        {
            return notificationChannelExt == NotificationChannelExt.EmailPreferred || notificationChannelExt == NotificationChannelExt.SmsPreferred;
        }
    }
}
