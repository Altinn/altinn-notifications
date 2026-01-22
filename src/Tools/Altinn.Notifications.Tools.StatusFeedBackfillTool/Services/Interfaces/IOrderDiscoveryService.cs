namespace Altinn.Notifications.Tools.StatusFeedBackfillTool.Services.Interfaces
{
    /// <summary>
    /// Service for discovering orders that are missing status feed entries.
    /// </summary>
    public interface IOrderDiscoveryService
    {
        /// <summary>
        /// Discovers affected orders and saves them to a configured file for review.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Run();
    }
}
