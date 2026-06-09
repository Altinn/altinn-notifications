namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Interface for notification repository operations.
    /// </summary>
    public interface INotificationRepository
    {
        /// <summary>
        /// Terminates notifications that have expired beyond the grace period.
        /// Configurable grace period is set by the 'ExpiryOffsetSeconds' setting.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TerminateExpiredNotifications();
    }
}
