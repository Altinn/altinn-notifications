using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Notification
{
    /// <summary>
    /// Interface describing a base notification.
    /// </summary>
    public interface INotification
    {
        /// <summary>
        /// Gets the id of the notification.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets the order id of the notification.
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// Gets the send time of the notification.
        /// </summary>
        public DateTime SendTime { get; set; }

        /// <summary>
        /// Gets the notifiction channel for the notification.
        /// </summary>
        public NotificationChannel NotificationChannel { get; set; }
    }
}