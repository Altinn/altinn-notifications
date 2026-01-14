namespace Altinn.Notifications.Tools.StatusFeedBackfillTool.Services.Interfaces
{
    public interface ITestDataService
    {
        Task GenerateTestData();
        Task CleanupTestData();
    }
}
