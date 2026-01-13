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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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
    public async Task Run_WithEmptyFile_LogsNoOrdersFound()
    {
        // Arrange
        var orderIds = new List<Guid>();
        await File.WriteAllTextAsync(_testFilePath, JsonSerializer.Serialize(orderIds));

        var orderRepo = TestServiceUtil.GetService<OrderRepository>();
        var mockLogger = new Mock<ILogger<StatusFeedBackfillService>>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = true
        });

        var service = new StatusFeedBackfillService(
            orderRepo,
            settings,
            mockLogger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No orders found in file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        var mockLogger = new Mock<ILogger<StatusFeedBackfillService>>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = true
        });

        var service = new StatusFeedBackfillService(
            backfillOrderRepo,
            settings,
            mockLogger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - should not have created status feed entries
        int count1 = await TestDataUtil.GetStatusFeedEntryCount(order1Id);
        int count2 = await TestDataUtil.GetStatusFeedEntryCount(order2Id);
        Assert.Equal(0, count1);
        Assert.Equal(0, count2);

        // Verify summary was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Backfill Summary")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
        var mockLogger = new Mock<ILogger<StatusFeedBackfillService>>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = false
        });

        var service = new StatusFeedBackfillService(
            backfillOrderRepo,
            settings,
            mockLogger.Object);

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
    public async Task Run_WithNonExistentFile_LogsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid()}.json");

        var orderRepo = TestServiceUtil.GetService<OrderRepository>();
        var mockLogger = new Mock<ILogger<StatusFeedBackfillService>>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = nonExistentPath,
            DryRun = true
        });

        var service = new StatusFeedBackfillService(
            orderRepo,
            settings,
            mockLogger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("File not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithNonExistentOrder_LogsErrorAndContinues()
    {
        // Arrange
        var nonExistentOrderId = Guid.NewGuid();
        var existingOrderId = await TestDataUtil.CreateSmsOrder("error-test");

        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        await orderRepo.SetProcessingStatus(existingOrderId, OrderProcessingStatus.Completed);

        var orderIds = new List<Guid> { nonExistentOrderId, existingOrderId };
        await File.WriteAllTextAsync(_testFilePath, JsonSerializer.Serialize(orderIds));

        var backfillOrderRepo = TestServiceUtil.GetService<OrderRepository>();
        var mockLogger = new Mock<ILogger<StatusFeedBackfillService>>();
        var settings = Options.Create(new BackfillSettings
        {
            OrderIdsFilePath = _testFilePath,
            DryRun = false
        });

        var service = new StatusFeedBackfillService(
            backfillOrderRepo,
            settings,
            mockLogger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - error should be logged for non-existent order
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing order")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // But the existing order should still be processed successfully
        await Task.Delay(2100);
        int count = await TestDataUtil.GetStatusFeedEntryCount(existingOrderId);
        Assert.Equal(1, count);
    }
}
