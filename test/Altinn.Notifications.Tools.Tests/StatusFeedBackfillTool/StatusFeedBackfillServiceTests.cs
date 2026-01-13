using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Repository;
using Altinn.Notifications.Tools.Tests.Utils;
using Microsoft.Extensions.Options;
using StatusFeedBackfillTool.Configuration;
using StatusFeedBackfillTool.Services;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.StatusFeedBackfillTool;

public class StatusFeedBackfillServiceTests : IAsyncLifetime
{
    private readonly string _testFilePath;

    public StatusFeedBackfillServiceTests()
    {
        // Create a unique test file path in the temp directory (cross-platform)
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test-orders-{Guid.NewGuid()}.json");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up file
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }

        // Clean up all test data (created with "ttd" creator)
        await TestDataUtil.CleanupTestData();

        // Note: Don't dispose TestServiceUtil here - xUnit runs tests in parallel
        // and they share the same data source
    }

    [Fact]
    public async Task Run_WithEmptyFile_CompletesSuccessfullyWithoutProcessing()
    {
        // Arrange
        var orderIds = new List<Guid>();
        await File.WriteAllTextAsync(_testFilePath, JsonSerializer.Serialize(orderIds));

        var orderRepo = TestServiceUtil.GetService<OrderRepository>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = true
        });

        var service = new StatusFeedBackfillService(orderRepo, settings);

        // Act & Assert - Should complete without throwing exception
        // Service writes "No orders found in file" to console and returns gracefully
        await service.Run(CancellationToken.None);

        // Verify file still exists and is still empty (no modifications)
        Assert.True(File.Exists(_testFilePath));
        var fileContent = await File.ReadAllTextAsync(_testFilePath);
        var deserializedOrders = JsonSerializer.Deserialize<List<Guid>>(fileContent);
        Assert.Empty(deserializedOrders!);
    }

    [Fact]
    public async Task Run_WithOrdersInDryRunMode_DoesNotInsertData()
    {
        // Arrange - Create test orders
        var order1Id = await TestDataUtil.CreateSmsOrder("dryrun-test-1");
        var order2Id = await TestDataUtil.CreateSmsOrder("dryrun-test-2");

        // Mark orders as Completed
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        await orderRepo.SetProcessingStatus(order1Id, OrderProcessingStatus.Completed);
        await orderRepo.SetProcessingStatus(order2Id, OrderProcessingStatus.Completed);

        var orderIds = new List<Guid> { order1Id, order2Id };
        await File.WriteAllTextAsync(_testFilePath, JsonSerializer.Serialize(orderIds));

        var backfillOrderRepo = TestServiceUtil.GetService<OrderRepository>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = true
        });

        var service = new StatusFeedBackfillService(backfillOrderRepo, settings);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - should not have created status feed entries
        int count1 = await TestDataUtil.GetStatusFeedEntryCount(order1Id);
        int count2 = await TestDataUtil.GetStatusFeedEntryCount(order2Id);
        Assert.Equal(0, count1);
        Assert.Equal(0, count2);
    }

    [Fact]
    public async Task Run_WithOrdersNotInDryRunMode_InsertsData()
    {
        // Arrange - Create test orders (legacy scenario without statusfeed)
        var order1Id = await TestDataUtil.CreateSmsOrder("insert-test-1");
        var order2Id = await TestDataUtil.CreateSmsOrder("insert-test-2");

        // Use legacy update to mark orders as Completed without automatic statusfeed creation
        await TestDataUtil.UpdateOrderStatusLegacy(order1Id, OrderProcessingStatus.Completed);
        await TestDataUtil.UpdateOrderStatusLegacy(order2Id, OrderProcessingStatus.Completed);

        // When tests run in sequence, give the database extra time to ensure consistency
        // This is necessary because we're using direct SQL updates that bypass the normal repository
        await Task.Delay(500);

        var orderIds = new List<Guid> { order1Id, order2Id };
        await File.WriteAllTextAsync(_testFilePath, JsonSerializer.Serialize(orderIds));

        var backfillOrderRepo = TestServiceUtil.GetService<OrderRepository>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = false
        });

        var service = new StatusFeedBackfillService(backfillOrderRepo, settings);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - should have created status feed entries
        // Note: SelectStatusFeedEntryCount may return 0 for entries created within the last 2 seconds
        // due to filtering in GetStatusFeed, so we wait a bit
        await Task.Delay(2100); // Wait for status feed entries to be selectable
        
        int count1 = await TestDataUtil.GetStatusFeedEntryCount(order1Id);
        int count2 = await TestDataUtil.GetStatusFeedEntryCount(order2Id);
        
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public async Task Run_WithNonExistentFile_HandlesErrorGracefully()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid()}.json");

        var orderRepo = TestServiceUtil.GetService<OrderRepository>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = nonExistentPath,
            DryRun = true
        });

        var service = new StatusFeedBackfillService(orderRepo, settings);

        // Act & Assert - Should complete without throwing exception
        // Service writes "ERROR: File not found" to console and returns gracefully without crashing
        await service.Run(CancellationToken.None);

        // Verify file was never created (service doesn't attempt to create missing files)
        Assert.False(File.Exists(nonExistentPath));
    }

    [Fact]
    public async Task Run_WithInvalidOrder_ContinuesProcessingRemainingOrders()
    {
        // Arrange
        var nonExistentOrderId = Guid.NewGuid();
        var existingOrderId = await TestDataUtil.CreateSmsOrder("error-test");

        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        await orderRepo.SetProcessingStatus(existingOrderId, OrderProcessingStatus.Completed);

        var orderIds = new List<Guid> { nonExistentOrderId, existingOrderId };
        await File.WriteAllTextAsync(_testFilePath, JsonSerializer.Serialize(orderIds));

        var backfillOrderRepo = TestServiceUtil.GetService<OrderRepository>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = false
        });

        var service = new StatusFeedBackfillService(backfillOrderRepo, settings);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Service logs error for invalid order but continues processing valid orders
        // Verify that the valid order was still processed successfully
        await Task.Delay(2100);
        int count = await TestDataUtil.GetStatusFeedEntryCount(existingOrderId);
        Assert.Equal(1, count);
    }
}
