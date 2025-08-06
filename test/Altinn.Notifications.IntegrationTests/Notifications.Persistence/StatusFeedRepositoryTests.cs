using System.Collections.Immutable;
using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class StatusFeedRepositoryTests : IAsyncLifetime
{
    private readonly string _creatorName = "testcase";

    public async Task DisposeAsync()
    {
        string deleteSql = $@"DELETE from notifications.statusfeed s where s.creatorname = '{_creatorName}'";
        await PostgreUtil.RunSql(deleteSql);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetStatusFeed_WithTestCreatorName_ReturnsExpectedResult()
    {
        // Arrange
        await InsertTestDataRowForStatusFeed(123, "2025-5-21", Guid.NewGuid());

        StatusFeedRepository statusFeedRepository = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));

        // Act
        var results = await statusFeedRepository.GetStatusFeed(0, _creatorName, CancellationToken.None);

        // Assert
        var item = Assert.Single(results);
        Assert.True(item.SequenceNumber > 0);
        Assert.Equal(Altinn.Notifications.Core.Enums.ProcessingLifecycle.Order_Completed, item.OrderStatus.Status);
    }

    [Fact]
    public async Task GetStatusFeed_EmptyCreatorName_ReturnsEmptyResult()
    {
        // Arrange
        StatusFeedRepository statusFeedRepository = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));
        
        // Act
        var results = await statusFeedRepository.GetStatusFeed(1, string.Empty, CancellationToken.None);
        
        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task DeleteOldStatusFeedRecords_DeletesRowsOlderThan90DaysOnly()
    {
        // Arrange
        StatusFeedRepository sut = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));

        int oldOrderId = 1001;
        Guid oldShipmentId = Guid.NewGuid();
        int recentOrderId = 1002;
        Guid recentShipmentId = Guid.NewGuid();

        string oldDate = DateTime.UtcNow.AddDays(-91).ToString("yyyy-MM-dd");
        string recentDate = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd");

        await InsertTestDataRowForStatusFeed(oldOrderId, oldDate, oldShipmentId);
        await InsertTestDataRowForStatusFeed(recentOrderId, recentDate, recentShipmentId);

        // Act
        var rowsAffected = await sut.DeleteOldStatusFeedRecords(CancellationToken.None);

        // Assert
        Assert.Equal(1, rowsAffected); // Only the old row should be deleted

        // Additional verification: ensure old record is gone, recent remains
        var remaining = await sut.GetStatusFeed(0, _creatorName, CancellationToken.None);
        Assert.DoesNotContain(remaining, x => x.OrderStatus.ShipmentId == oldShipmentId);
        Assert.Contains(remaining, x => x.OrderStatus.ShipmentId == recentShipmentId);
    }

    private async Task InsertTestDataRowForStatusFeed(int orderId, string created, Guid shipmentId)
    {
        OrderStatus orderStatus = new OrderStatus
        {
            Status = ProcessingLifecycle.Order_Completed,
            ShipmentId = shipmentId,
            LastUpdated = DateTime.UtcNow,
            ShipmentType = "Notification",
            SendersReference = Guid.NewGuid().ToString(),
            Recipients = new List<Recipient>
            {
                new()
                {
                    LastUpdate = DateTime.UtcNow,
                    Status = ProcessingLifecycle.Email_Delivered,
                    Destination = "+4799999999"
                }
            }.ToImmutableList(),
        };

        var orderStatusFeedTestOrderCompleted = JsonSerializer.Serialize(orderStatus);

        var sqlInsert = $@"INSERT INTO notifications.statusfeed(
                              orderid, creatorname, created, orderstatus)
                              VALUES({orderId}, '{_creatorName}', '{created}', '{orderStatusFeedTestOrderCompleted}')";

        await PostgreUtil.RunSql(sqlInsert);
    }
}
