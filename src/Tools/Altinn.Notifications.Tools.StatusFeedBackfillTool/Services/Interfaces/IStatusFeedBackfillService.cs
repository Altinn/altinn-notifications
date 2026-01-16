namespace Altinn.Notifications.Tools.StatusFeedBackfillTool.Services.Interfaces
{
    /// <summary>
    /// Service for backfilling missing status feed entries for orders.
    /// </summary>
    public interface IStatusFeedBackfillService
    {
        /// <summary>
        /// Reads order IDs from a configured file and inserts missing status feed entries.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Run();
    }
}
