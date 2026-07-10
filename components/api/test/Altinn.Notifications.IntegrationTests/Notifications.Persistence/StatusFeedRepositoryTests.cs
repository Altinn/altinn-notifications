using System.Collections.Immutable;
using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class StatusFeedRepositoryTests : IAsyncLifetime
{
    private const int _maxPageSize = 500;
    private readonly List<Guid> _ordersToDelete = [];
    private readonly List<int> _fakeOrderIdsToDelete = [];
    private readonly string _creatorName = $"ttd-{Guid.NewGuid():N}";

    public async ValueTask DisposeAsync()
    {
        foreach (var orderId in _ordersToDelete)
        {
            await PostgreUtil.DeleteStatusFeedFromDb(orderId);
            await PostgreUtil.DeleteNotificationLogFromDb(orderId);
            await PostgreUtil.DeleteOrderFromDb(orderId);
        }

        foreach (var fakeOrderId in _fakeOrderIdsToDelete)
        {
            await PostgreUtil.RunSql($"DELETE FROM notifications.statusfeed WHERE orderid = {fakeOrderId}");
        }
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetStatusFeed_WithTestCreatorName_ReturnsExpectedResult()
    {
        // Arrange
        var fakeOrderId = 123;
        var shipmentId = Guid.NewGuid();
        await InsertTestDataRowForStatusFeed(fakeOrderId, "2025-5-21", shipmentId);
        _fakeOrderIdsToDelete.Add(fakeOrderId);

        StatusFeedRepository statusFeedRepository = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));

        // Act
        var results = await statusFeedRepository.GetStatusFeed(0, _creatorName, "asc", _maxPageSize, TestContext.Current.CancellationToken);
        var filteredByShipmentId = results.Where(x => x.OrderStatus.ShipmentId == shipmentId);

        // Assert
        var item = Assert.Single(filteredByShipmentId);
        Assert.True(item.SequenceNumber > 0);
        Assert.Equal(ProcessingLifecycle.Order_Completed, item.OrderStatus.Status);
    }

    [Fact]
    public async Task GetStatusFeed_EmptyCreatorName_ReturnsEmptyResult()
    {
        // Arrange
        StatusFeedRepository statusFeedRepository = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));
        
        // Act
        var results = await statusFeedRepository.GetStatusFeed(1, string.Empty, "asc", _maxPageSize, TestContext.Current.CancellationToken);
        
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
        _fakeOrderIdsToDelete.Add(oldOrderId);
        await InsertTestDataRowForStatusFeed(recentOrderId, recentDate, recentShipmentId);
        _fakeOrderIdsToDelete.Add(recentOrderId);
        
        // Act
        var rowsAffected = await sut.DeleteOldStatusFeedRecords(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, rowsAffected); // Only the old row should be deleted

        // Additional verification: ensure old record is gone, recent remains
        var remaining = await sut.GetStatusFeed(0, _creatorName, "asc", _maxPageSize, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(remaining, x => x.OrderStatus.ShipmentId == oldShipmentId);
        Assert.Contains(remaining, x => x.OrderStatus.ShipmentId == recentShipmentId);
    }

    [Fact]
    public async Task InsertStatusFeedEntry_NullOrderStatus_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await StatusFeedRepository.InsertStatusFeedEntry(null!, null!, null!));
    }

    [Fact]
    public async Task InsertStatusFeedEntry_NullConnection_ThrowsArgumentNullException()
    {
        // Arrange
        OrderStatus orderStatus = new()
        {
            Status = ProcessingLifecycle.Order_SendConditionNotMet,
            ShipmentId = Guid.NewGuid(),
            LastUpdated = DateTime.UtcNow,
            ShipmentType = "Notification",
            SendersReference = Guid.NewGuid().ToString(),
            Recipients = new List<Recipient>().ToImmutableList()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await StatusFeedRepository.InsertStatusFeedEntry(orderStatus, null!, null!));
    }

    [Fact]
    public async Task InsertStatusFeedEntry_OrderDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        Guid nonExistentOrderId = Guid.NewGuid();
        OrderStatus orderStatus = new()
        {
            Status = ProcessingLifecycle.Order_SendConditionNotMet,
            ShipmentId = nonExistentOrderId,
            LastUpdated = DateTime.UtcNow,
            ShipmentType = "Notification",
            SendersReference = Guid.NewGuid().ToString(),
            Recipients = new List<Recipient>().ToImmutableList()
        };

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await StatusFeedRepository.InsertStatusFeedEntry(orderStatus, connection, transaction));

        Assert.Contains("Failed to insert status feed entry", exception.Message);
        Assert.Contains(nonExistentOrderId.ToString(), exception.Message);
    }

    [Fact]
    public async Task InsertStatusFeedEntry_ValidOrder_InsertsSuccessfully()
    {
        // Arrange
        Guid orderAlternateId = await PostgreUtil.PopulateDBWithEmailOrderAndReturnId();

        OrderStatus orderStatus = new()
        {
            Status = ProcessingLifecycle.Order_SendConditionNotMet,
            ShipmentId = orderAlternateId,
            LastUpdated = DateTime.UtcNow,
            ShipmentType = "Notification",
            SendersReference = Guid.NewGuid().ToString(),
            Recipients = new List<Recipient>
            {
                new()
                {
                    LastUpdate = DateTime.UtcNow,
                    Status = ProcessingLifecycle.Email_New,
                    Destination = "test@example.com"
                }
            }.ToImmutableList()
        };

        NpgsqlDataSource dataSource = (NpgsqlDataSource)ServiceUtil.GetServices([typeof(NpgsqlDataSource)])[0]!;
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        await StatusFeedRepository.InsertStatusFeedEntry(orderStatus, connection, transaction);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert - verify status feed entry was created
        // Note: We use SelectStatusFeedEntryCount instead of GetStatusFeed because GetStatusFeed
        // filters out entries created within the last 2 seconds to avoid returning entries still being processed
        int statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(orderAlternateId);
        Assert.Equal(1, statusFeedCount);

        // Cleanup
        _ordersToDelete.Add(orderAlternateId);
    }

    [Fact]
    public async Task DeleteOldStatusFeedRecords_RespectsConfiguredBatchSize()
    {
        int batchSize = 2;
        var oldDate = DateTime.UtcNow.AddDays(-91).ToString("yyyy-MM-dd");

        var shipmentIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var fakeOrderIds = new List<int> { 2001, 2002, 2003 };

        for (int i = 0; i < 3; i++)
        {
            await InsertTestDataRowForStatusFeed(fakeOrderIds[i], oldDate, shipmentIds[i]);
            _fakeOrderIdsToDelete.Add(fakeOrderIds[i]);
        }

        StatusFeedRepository sut = BuildRepositoryWithBatchSize(batchSize);

        // Act — first invocation
        var firstRun = await sut.DeleteOldStatusFeedRecords(TestContext.Current.CancellationToken);

        // Assert — batch limit was respected: at most batchSize rows deleted overall,
        // so at least one of *our* rows must still remain
        Assert.True(firstRun <= batchSize, $"Expected at most {batchSize} rows deleted, got {firstRun}");
        int remainingAfterFirst = await CountRemainingRows(fakeOrderIds);
        Assert.True(remainingAfterFirst > 0, "Expected at least one owned row to survive the first batch");

        // Act — second invocation (and beyond, for safety)
        while (await CountRemainingRows(fakeOrderIds) > 0)
        {
            await sut.DeleteOldStatusFeedRecords(TestContext.Current.CancellationToken);
        }

        // Assert — all owned rows eventually cleaned up
        Assert.Equal(0, await CountRemainingRows(fakeOrderIds));
    }

    [Fact]
    public async Task GetStatusFeed_NullJsonOrderStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        int fakeOrderId = 9999;
        _fakeOrderIdsToDelete.Add(fakeOrderId);

        // Insert a row with JSON 'null' as orderstatus — valid JSONB but deserializes to null
        var sqlInsert = $@"INSERT INTO notifications.statusfeed(
                          orderid, creatorname, created, orderstatus)
                          VALUES({fakeOrderId}, '{_creatorName}', '2025-01-01', 'null'::jsonb)";
        await PostgreUtil.RunSql(sqlInsert);

        StatusFeedRepository sut = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.GetStatusFeed(0, _creatorName, "asc", _maxPageSize, TestContext.Current.CancellationToken));

        Assert.Contains("Deserialized OrderStatus is null for sequence number", exception.Message);
    }

    [Fact]
    public void Constructor_InvalidBatchSize_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => BuildRepositoryWithBatchSize(0));

        Assert.Contains("StatusFeedCleanupBatchSize must be greater than 0", exception.Message);
    }

    [Fact]
    public async Task GetStatusFeed_DescendingWithSeqZero_ReturnsMostRecentEntriesInDescendingOrder()
    {
        // Arrange — seq=0 + "desc" is the latest-tail path: no lower bound applied, most recent entries returned
        var shipmentId1 = Guid.NewGuid();
        var shipmentId2 = Guid.NewGuid();
        var shipmentId3 = Guid.NewGuid();
        var knownShipmentIds = new HashSet<Guid> { shipmentId1, shipmentId2, shipmentId3 };

        var oldDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        await InsertTestDataRowForStatusFeed(4001, oldDate, shipmentId1);
        _fakeOrderIdsToDelete.Add(4001);
        await InsertTestDataRowForStatusFeed(4002, oldDate, shipmentId2);
        _fakeOrderIdsToDelete.Add(4002);
        await InsertTestDataRowForStatusFeed(4003, oldDate, shipmentId3);
        _fakeOrderIdsToDelete.Add(4003);

        StatusFeedRepository sut = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));

        // Act
        var results = await sut.GetStatusFeed(0, _creatorName, "desc", _maxPageSize, TestContext.Current.CancellationToken);
        var filtered = results.Where(x => knownShipmentIds.Contains(x.OrderStatus.ShipmentId)).ToList();

        // Assert — all three rows returned and in strictly descending sequence-number order
        Assert.Equal(3, filtered.Count);
        for (int i = 0; i < filtered.Count - 1; i++)
        {
            Assert.True(
                filtered[i].SequenceNumber > filtered[i + 1].SequenceNumber,
                $"Expected descending order at index {i}: seq {filtered[i].SequenceNumber} should be > seq {filtered[i + 1].SequenceNumber}");
        }
    }

    [Fact]
    public async Task GetStatusFeed_DescendingWithCursor_ReturnsPriorEntriesWithNoOverlapAcrossPages()
    {
        // Arrange — insert 4 rows so we can page through them two at a time
        var shipmentIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var fakeOrderIds = new List<int> { 4004, 4005, 4006, 4007 };
        var oldDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        foreach (var (orderId, shipmentId) in fakeOrderIds.Zip(shipmentIds))
        {
            await InsertTestDataRowForStatusFeed(orderId, oldDate, shipmentId);
            _fakeOrderIdsToDelete.Add(orderId);
        }

        StatusFeedRepository sut = (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)])
            .First(i => i.GetType() == typeof(StatusFeedRepository));

        // Act — first page: seq=0 returns the 2 most recent entries (latest-tail)
        var firstPage = await sut.GetStatusFeed(0, _creatorName, "desc", 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, firstPage.Count);

        // The last entry of the first page is the oldest on that page; use it as the cursor
        long cursor = firstPage[^1].SequenceNumber;

        // Act — second page: seq=cursor returns entries with _id < cursor
        var secondPage = await sut.GetStatusFeed(cursor, _creatorName, "desc", 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, secondPage.Count);

        // Both pages are each in descending order internally
        Assert.True(firstPage[0].SequenceNumber > firstPage[1].SequenceNumber);
        Assert.True(secondPage[0].SequenceNumber > secondPage[1].SequenceNumber);

        // There is a clean boundary: every entry on page 1 is newer than every entry on page 2
        Assert.True(
            firstPage.Min(e => e.SequenceNumber) > secondPage.Max(e => e.SequenceNumber),
            "All first-page entries should have higher sequence numbers than all second-page entries");

        // All second-page entries are strictly below the cursor
        Assert.All(secondPage, e => Assert.True(e.SequenceNumber < cursor, $"Second-page seq {e.SequenceNumber} should be < cursor {cursor}"));

        // Together both pages cover all four inserted rows with no overlap
        var firstPageShipmentIds = firstPage.Select(x => x.OrderStatus.ShipmentId).ToHashSet();
        var secondPageShipmentIds = secondPage.Select(x => x.OrderStatus.ShipmentId).ToHashSet();
        Assert.Empty(firstPageShipmentIds.Intersect(secondPageShipmentIds));
        Assert.True(
            firstPageShipmentIds.Union(secondPageShipmentIds).ToHashSet().SetEquals(shipmentIds),
            $"Expected combined pages to cover exactly the {shipmentIds.Count} inserted shipment IDs");
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

    private static StatusFeedRepository BuildRepositoryWithBatchSize(int batchSize)
    {
        var configOverrides = new Dictionary<string, string>
        {
            { "NotificationConfig__StatusFeedCleanupBatchSize", batchSize.ToString() }
        };

        return (StatusFeedRepository)ServiceUtil
            .GetServices([typeof(IStatusFeedRepository)], configOverrides)
            .First(i => i.GetType() == typeof(StatusFeedRepository));
    }

    private static async Task<int> CountRemainingRows(IEnumerable<int> fakeOrderIds)
    {
        var ids = string.Join(",", fakeOrderIds);
        return await PostgreUtil.RunSqlReturnOutput<int>(
            $"SELECT COUNT(*)::int FROM notifications.statusfeed WHERE orderid IN ({ids})");
    }
}
