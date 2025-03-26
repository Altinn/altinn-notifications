using System.Runtime.CompilerServices;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Validators.Extensions
{
    /// <summary>
    /// Extension class for <see cref="NotificationChannelExt"/>
    /// </summary>
    public static class NotificationChannelExtensions
    {
        /// <summary>
        /// Determines whether the specified notification channel is a preferred schema.
        /// </summary>
        /// <param name="notificationChannelExt">The notification channel to check.</param>
        /// <returns>True if the notification channel is preferred; otherwise, false.</returns>
        public static bool IsPreferredSchema(this NotificationChannelExt notificationChannelExt)
        {
            return notificationChannelExt == NotificationChannelExt.EmailPreferred || notificationChannelExt == NotificationChannelExt.SmsPreferred;
        }
    }
}
