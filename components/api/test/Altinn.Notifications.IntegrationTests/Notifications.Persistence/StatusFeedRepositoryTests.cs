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

public class StatusFeedRepositoryTests : IAsyncLifetime
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
            await PostgreUtil.DeleteOrderFromDb(orderId);
        }

        foreach (var fakeOrderId in _fakeOrderIdsToDelete)
        {
            await PostgreUtil.RunSql($"DELETE FROM notifications.statusfeed WHERE orderid = {fakeOrderId}");
        }

        GC.SuppressFinalize(this);
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
        var results = await statusFeedRepository.GetStatusFeed(0, _creatorName, _maxPageSize, CancellationToken.None);
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
        var results = await statusFeedRepository.GetStatusFeed(1, string.Empty, _maxPageSize, CancellationToken.None);
        
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
        var rowsAffected = await sut.DeleteOldStatusFeedRecords(CancellationToken.None);

        // Assert
        Assert.Equal(1, rowsAffected); // Only the old row should be deleted

        // Additional verification: ensure old record is gone, recent remains
        var remaining = await sut.GetStatusFeed(0, _creatorName, _maxPageSize, CancellationToken.None);
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
        // Arrange — insert more old rows than the configured batch size
        // (requires a small batch size set via test config, e.g. 2)
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
        var firstRun = await sut.DeleteOldStatusFeedRecords(CancellationToken.None);

        // Assert — only batch_size rows deleted, one remains
        Assert.Equal(batchSize, firstRun);

        // Act — second invocation
        var secondRun = await sut.DeleteOldStatusFeedRecords(CancellationToken.None);

        // Assert — remaining row deleted
        Assert.Equal(1, secondRun);
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
            async () => await sut.GetStatusFeed(0, _creatorName, _maxPageSize, CancellationToken.None));

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
        string? previousValue = Environment.GetEnvironmentVariable("NotificationConfig__StatusFeedCleanupBatchSize");
        var envVariables = new Dictionary<string, string>
        {
            { "NotificationConfig__StatusFeedCleanupBatchSize", batchSize.ToString() }
        };

        try
        {
            return (StatusFeedRepository)ServiceUtil
                .GetServices([typeof(IStatusFeedRepository)], envVariables)
                .First(i => i.GetType() == typeof(StatusFeedRepository));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NotificationConfig__StatusFeedCleanupBatchSize", previousValue);
        }
    }
}
