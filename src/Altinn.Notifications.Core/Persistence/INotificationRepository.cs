namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Interface for notification repository operations.
    /// </summary>
    public interface INotificationRepository
    {
        /// <summary>
        /// Terminates notifications that have expired.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TerminateExpiredNotifications();
    }
}
