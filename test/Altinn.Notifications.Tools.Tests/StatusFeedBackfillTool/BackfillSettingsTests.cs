using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;

using StatusFeedBackfillTool;

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
        Assert.Equal(100, settings.BatchSize);
        Assert.True(settings.DryRun);
        Assert.Null(settings.CreatorNameFilter);
        Assert.Null(settings.MinProcessedDate);
        Assert.Null(settings.OrderProcessingStatusFilter);
        Assert.Null(settings.OrderIds);
    }

    [Fact]
    public void BackfillSettings_CanSetProperties()
    {
        // Arrange
        var orderIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var settings = new BackfillSettings
        {
            BatchSize = 50,
            DryRun = false,
            CreatorNameFilter = "test-creator",
            MinProcessedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OrderProcessingStatusFilter = OrderProcessingStatus.SendConditionNotMet,
            OrderIds = orderIds
        };

        // Assert
        Assert.Equal(50, settings.BatchSize);
        Assert.False(settings.DryRun);
        Assert.Equal("test-creator", settings.CreatorNameFilter);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings.MinProcessedDate);
        Assert.Equal(OrderProcessingStatus.SendConditionNotMet, settings.OrderProcessingStatusFilter);
        Assert.Equal(orderIds, settings.OrderIds);
    }
}
