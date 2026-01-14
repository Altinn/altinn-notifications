using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Tools.Tests.Utils;
using Microsoft.Extensions.Options;
using Npgsql;
using StatusFeedBackfillTool.Configuration;
using StatusFeedBackfillTool.Services;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.StatusFeedBackfillTool;

public class OrderDiscoveryServiceTests : IAsyncLifetime
{
    private readonly string _testFilePath;

    public OrderDiscoveryServiceTests()
    {
        // Create a unique test file path in the temp directory (cross-platform)
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test-discovered-orders-{Guid.NewGuid()}.json");
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
    public async Task SaveOrdersToFile_CreatesValidJsonFile()
    {
        // Arrange
        var orderIds = new List<Guid>
        {
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8")
        };

        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            MaxOrders = 100
        });

        // Create service using reflection to access private SaveOrdersToFile method
        var service = new OrderDiscoveryService(null!, settings);

        // Use reflection to call private method
        var method = typeof(OrderDiscoveryService).GetMethod(
            "SaveOrdersToFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)method!.Invoke(service, [orderIds])!;

        // Assert
        Assert.True(File.Exists(_testFilePath));

        var json = await File.ReadAllTextAsync(_testFilePath);
        var deserializedOrders = JsonSerializer.Deserialize<List<Guid>>(json);

        Assert.NotNull(deserializedOrders);
        Assert.Equal(2, deserializedOrders.Count);
        Assert.Contains(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), deserializedOrders);
        Assert.Contains(Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"), deserializedOrders);
    }

    [Fact]
    public async Task DiscoverOrders_CompletedWithStatusFeed_NotFound()
    {
        // Arrange - Create Completed order WITH status feed entry (automatic via UpdateSendStatus)
        var (orderId, emailId) = await TestDataUtil.CreateEmailOrder("completed-with-sf");
        
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        var emailRepo = TestServiceUtil.GetService<IEmailNotificationRepository>();
        
        // Keep order in Processing state so UpdateSendStatus can complete it and create statusfeed
        await orderRepo.SetProcessingStatus(orderId, OrderProcessingStatus.Processing);
        await emailRepo.UpdateSendStatus(emailId, EmailNotificationResultType.Delivered);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should NOT be found (already has status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        
        Assert.DoesNotContain(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_CompletedWithoutStatusFeed_Found()
    {
        // Arrange - Create Completed order WITHOUT status feed entry (legacy scenario)
        var (orderId, _) = await TestDataUtil.CreateEmailOrder("completed-without-sf");
        
        // Use legacy update to avoid automatic statusfeed creation
        await TestDataUtil.UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.Completed);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order SHOULD be found (missing status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_SendConditionNotMetWithStatusFeed_NotFound()
    {
        // Arrange - Create SendConditionNotMet order WITH status feed entry
        var orderId = await TestDataUtil.CreateSmsOrder("scnm-with-sf");
        
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        await orderRepo.SetProcessingStatus(orderId, OrderProcessingStatus.SendConditionNotMet);
        
        // Repository method automatically creates statusfeed for final states
        await orderRepo.InsertStatusFeedForOrder(orderId);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should NOT be found (already has status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        
        Assert.DoesNotContain(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_SendConditionNotMetWithoutStatusFeed_Found()
    {
        // Arrange - Create SendConditionNotMet order WITHOUT status feed entry (legacy scenario)
        var orderId = await TestDataUtil.CreateSmsOrder("scnm-without-sf");
        
        // Use legacy update to avoid automatic statusfeed creation
        await TestDataUtil.UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.SendConditionNotMet);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order SHOULD be found (missing status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_ProcessingStatus_NotFound()
    {
        // Arrange - Create Processing order WITHOUT status feed entry
        var (orderId, _) = await TestDataUtil.CreateEmailOrder("processing");
        
        // Keep order in Processing state
        await TestDataUtil.UpdateOrderStatus(orderId, OrderProcessingStatus.Processing);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should NOT be found (not a final status)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        
        Assert.DoesNotContain(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_RegisteredStatus_NotFound()
    {
        // Arrange - Create Registered order WITHOUT status feed entry
        var orderId = await TestDataUtil.CreateSmsOrder("registered");
        
        // Order defaults to Registered status
        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should NOT be found (not a final status)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        
        Assert.DoesNotContain(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_WithDateFilter_OnlyFindsOrdersAfterDate()
    {
        // Arrange - Create orders with different processed dates
        // Order 1: Completed WITH status feed (establishes min date baseline)
        var (order1Id, email1Id) = await TestDataUtil.CreateEmailOrder("date-filter-1");
        
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        var emailRepo = TestServiceUtil.GetService<IEmailNotificationRepository>();
        
        // Keep in Processing so UpdateSendStatus can complete it and create statusfeed
        await orderRepo.SetProcessingStatus(order1Id, OrderProcessingStatus.Processing);
        await emailRepo.UpdateSendStatus(email1Id, EmailNotificationResultType.Delivered);
        
        await Task.Delay(100); // Ensure time difference
        
        // Order 2: Completed WITHOUT status feed, processed AFTER min date (should be found) - legacy scenario
        var (order2Id, _) = await TestDataUtil.CreateEmailOrder("date-filter-2");
        
        // Use legacy update to avoid automatic statusfeed creation
        await TestDataUtil.UpdateOrderStatusLegacy(order2Id, OrderProcessingStatus.Completed);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.DoesNotContain(order1Id, discoveredOrders); // Has status feed
        Assert.Contains(order2Id, discoveredOrders); // Missing status feed, processed after min date
    }

    [Fact]
    public async Task DiscoverOrders_WithStatusFilter_OnlyFindsMatchingStatus()
    {
        // Arrange - Create both SendConditionNotMet and Completed orders (legacy scenarios without statusfeed)
        var scnmOrderId = await TestDataUtil.CreateSmsOrder("status-filter-scnm");
        await TestDataUtil.UpdateOrderStatusLegacy(scnmOrderId, OrderProcessingStatus.SendConditionNotMet);
        
        var (completedOrderId, _) = await TestDataUtil.CreateEmailOrder("status-filter-completed");
        await TestDataUtil.UpdateOrderStatusLegacy(completedOrderId, OrderProcessingStatus.Completed);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            OrderProcessingStatusFilter = OrderProcessingStatus.SendConditionNotMet,
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Only SendConditionNotMet should be found
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(scnmOrderId, discoveredOrders);
        Assert.DoesNotContain(completedOrderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_CompletedWithOldProcessedStatus_StillFound()
    {
        // This test ensures we still find Completed orders even if they previously used the old "Processed" terminology
        // (testing backward compatibility / legacy data handling)
        
        // Arrange - Create legacy Completed order without statusfeed
        var (orderId, _) = await TestDataUtil.CreateEmailOrder("legacy-completed");

        // Use legacy update to avoid automatic statusfeed creation
        await TestDataUtil.UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.Completed);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should be found (Completed without status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_WithNoCreatorFilter_FindsAllCreators()
    {
        // Arrange - Create order without creator filter
        var (orderId, _) = await TestDataUtil.CreateEmailOrder("no-creator-filter");
        await TestDataUtil.UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.Completed);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = null, // No creator filter
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should be found (no creator restriction)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);

        Assert.NotNull(discoveredOrders);
        Assert.Contains(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_WithMinProcessedDateTimeFilter_UsesProvidedDate()
    {
        // Arrange - Create completed order without status feed
        var (orderId, _) = await TestDataUtil.CreateEmailOrder("min-date-filter");
        await TestDataUtil.UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.Completed);

        // Set MinProcessedDateTimeFilter to a date in the past to ensure order is found
        var minDate = DateTime.UtcNow.AddDays(-1);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MinProcessedDateTimeFilter = minDate,
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should be found (processed after provided min date)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);

        Assert.NotNull(discoveredOrders);
        Assert.Contains(orderId, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_WithStatusFilterAndMinDate_FindsMatchingOrders()
    {
        // Arrange - Create SendConditionNotMet order without status feed
        var orderId = await TestDataUtil.CreateSmsOrder("status-and-date-filter");
        await TestDataUtil.UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.SendConditionNotMet);

        var minDate = DateTime.UtcNow.AddDays(-1);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = "ttd",
            MinProcessedDateTimeFilter = minDate,
            OrderProcessingStatusFilter = OrderProcessingStatus.SendConditionNotMet,
            MaxOrders = 100
        });

        var service = new OrderDiscoveryService(dataSource, settings);

        // Act
        await service.Run();

        // Assert - Order should be found (matches status filter and date)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);

        Assert.NotNull(discoveredOrders);
        Assert.Contains(orderId, discoveredOrders);
    }
}
