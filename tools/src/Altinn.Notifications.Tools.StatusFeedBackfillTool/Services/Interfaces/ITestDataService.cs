namespace Altinn.Notifications.Tools.StatusFeedBackfillTool.Services.Interfaces
{
    /// <summary>
    /// Service for generating and cleaning up test data for manual testing of the backfill tool.
    /// </summary>
    public interface ITestDataService
    {
        /// <summary>
        /// Generates diverse test orders to verify the backfill tool's discovery and insertion logic.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GenerateTestData();

        /// <summary>
        /// Removes all test data created by this service.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CleanupTestData();
    }
}
