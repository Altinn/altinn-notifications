using System;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Tools.StatusFeedBackfillTool.Configuration;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.StatusFeedBackfillTool;

public class DiscoverySettingsTests
{
    [Fact]
    public void DiscoverySettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new DiscoverySettings();

        // Assert
        Assert.Equal("affected-orders.json", settings.OrderIdsFilePath);
        Assert.Equal(100, settings.MaxOrders);
        Assert.Null(settings.CreatorNameFilter);
        Assert.Null(settings.MinProcessedDateTimeFilter);
        Assert.Null(settings.OrderProcessingStatusFilter);
    }

    [Fact]
    public void DiscoverySettings_CanSetProperties()
    {
        // Arrange
        var settings = new DiscoverySettings
        {
            OrderIdsFilePath = "custom-orders.json",
            MaxOrders = 50,
            CreatorNameFilter = "test-creator",
            MinProcessedDateTimeFilter = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OrderProcessingStatusFilter = OrderProcessingStatus.Completed
        };

        // Assert
        Assert.Equal("custom-orders.json", settings.OrderIdsFilePath);
        Assert.Equal(50, settings.MaxOrders);
        Assert.Equal("test-creator", settings.CreatorNameFilter);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings.MinProcessedDateTimeFilter);
        Assert.Equal(OrderProcessingStatus.Completed, settings.OrderProcessingStatusFilter);
    }
}
