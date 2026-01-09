using StatusFeedBackfillTool.Configuration;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.StatusFeedBackfillTool;

public class BackfillSettingsTests
{
    [Fact]
    public void BackfillSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new BackfillSettings();

        // Assert
        Assert.Equal("affected-orders.json", settings.OrderIdsFilePath);
        Assert.True(settings.DryRun);
    }

    [Fact]
    public void BackfillSettings_CanSetProperties()
    {
        // Arrange
        var settings = new BackfillSettings
        {
            OrderIdsFilePath = "custom-orders.json",
            DryRun = false
        };

        // Assert
        Assert.Equal("custom-orders.json", settings.OrderIdsFilePath);
        Assert.False(settings.DryRun);
    }
}
