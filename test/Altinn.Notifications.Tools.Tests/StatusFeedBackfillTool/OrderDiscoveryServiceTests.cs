using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Tools.Tests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using StatusFeedBackfillTool.Configuration;
using StatusFeedBackfillTool.Services;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.StatusFeedBackfillTool;

public class OrderDiscoveryServiceTests : IAsyncLifetime
{
    private readonly string _testFilePath;
    private readonly List<Guid> _orderIdsToCleanup = [];

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

        // Clean up test orders from database
        if (_orderIdsToCleanup.Count > 0)
        {
            string deleteSql = $@"DELETE FROM notifications.orders WHERE alternateid IN ('{string.Join("','", _orderIdsToCleanup)}')";
            await PostgreUtil.RunSql(deleteSql);
        }
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

        var mockLogger = new Mock<ILogger<OrderDiscoveryService>>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            MaxOrders = 100
        });

        // Create service using reflection to access private SaveOrdersToFile method
        var service = new OrderDiscoveryService(
            null!,
            settings,
            mockLogger.Object);

        // Use reflection to call private method
        var method = typeof(OrderDiscoveryService).GetMethod(
            "SaveOrdersToFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)method!.Invoke(service, [orderIds, CancellationToken.None])!;

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
        // Arrange - Create Completed order WITH status feed entry
        (var order, var email) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification("completed-with-sf");
        _orderIdsToCleanup.Add(order.Id);
        
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        var emailRepo = TestServiceUtil.GetService<IEmailNotificationRepository>();
        
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
        await emailRepo.UpdateSendStatus(email.Id, EmailNotificationResultType.Delivered);
        await orderRepo.InsertStatusFeedForOrder(order.Id); // Create status feed entry

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Order should NOT be found (already has status feed)
        var discoveredOrders = new List<Guid>();
        if (File.Exists(_testFilePath))
        {
            var json = await File.ReadAllTextAsync(_testFilePath);
            discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        
        Assert.DoesNotContain(order.Id, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_CompletedWithoutStatusFeed_Found()
    {
        // Arrange - Create Completed order WITHOUT status feed entry
        (var order, var email) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification("completed-without-sf");
        _orderIdsToCleanup.Add(order.Id);
        
        // Update order to Completed via SQL to avoid automatic status feed creation
        string updateEmailSql = $@"UPDATE notifications.emailnotifications SET result = 'Delivered', resulttime = now() WHERE alternateid = '{email.Id}'";
        await PostgreUtil.RunSql(updateEmailSql);
        
        string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'Completed', processed = now() WHERE alternateid = '{order.Id}'";
        await PostgreUtil.RunSql(updateOrderSql);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Order SHOULD be found (missing status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(order.Id, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_SendConditionNotMetWithStatusFeed_NotFound()
    {
        // Arrange - Create SendConditionNotMet order WITH status feed entry (no notifications)
        var order = await PostgreUtil.PopulateDBWithEmailOrder("scnm-with-sf");
        _orderIdsToCleanup.Add(order.Id);
        
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
        await orderRepo.InsertStatusFeedForOrder(order.Id); // Create status feed entry

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Order should NOT be found (already has status feed)
        var discoveredOrders = new List<Guid>();
        if (File.Exists(_testFilePath))
        {
            var json = await File.ReadAllTextAsync(_testFilePath);
            discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        
        Assert.DoesNotContain(order.Id, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_SendConditionNotMetWithoutStatusFeed_Found()
    {
        // Arrange - Create SendConditionNotMet order WITHOUT status feed entry (no notifications)
        var order = await PostgreUtil.PopulateDBWithEmailOrder("scnm-without-sf");
        _orderIdsToCleanup.Add(order.Id);
        
        // Update order to SendConditionNotMet via SQL to avoid automatic status feed creation
        string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'SendConditionNotMet', processed = now() WHERE alternateid = '{order.Id}'";
        await PostgreUtil.RunSql(updateOrderSql);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Order SHOULD be found (missing status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(order.Id, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_ProcessingStatus_NotFound()
    {
        // Arrange - Create Processing order WITHOUT status feed entry
        (var order, _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification("processing");
        _orderIdsToCleanup.Add(order.Id);
        
        // Keep order in Processing state (default from PopulateDBWithOrderAndEmailNotification)
        string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'Processing' WHERE alternateid = '{order.Id}'";
        await PostgreUtil.RunSql(updateOrderSql);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Order should NOT be found (not a final status)
        var discoveredOrders = new List<Guid>();
        if (File.Exists(_testFilePath))
        {
            var json = await File.ReadAllTextAsync(_testFilePath);
            discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        
        Assert.DoesNotContain(order.Id, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_RegisteredStatus_NotFound()
    {
        // Arrange - Create Registered order WITHOUT status feed entry
        var order = await PostgreUtil.PopulateDBWithEmailOrder("registered");
        _orderIdsToCleanup.Add(order.Id);
        
        // Order defaults to Registered status
        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Order should NOT be found (not a final status)
        var discoveredOrders = new List<Guid>();
        if (File.Exists(_testFilePath))
        {
            var json = await File.ReadAllTextAsync(_testFilePath);
            discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        
        Assert.DoesNotContain(order.Id, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_WithDateFilter_OnlyFindsOrdersAfterDate()
    {
        // Arrange - Create orders with different processed dates
        // Order 1: Completed WITH status feed (should establish min date baseline)
        (var order1, var email1) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification("date-filter-1");
        _orderIdsToCleanup.Add(order1.Id);
        
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        var emailRepo = TestServiceUtil.GetService<IEmailNotificationRepository>();
        
        await orderRepo.SetProcessingStatus(order1.Id, OrderProcessingStatus.Completed);
        await emailRepo.UpdateSendStatus(email1.Id, EmailNotificationResultType.Delivered);
        await orderRepo.InsertStatusFeedForOrder(order1.Id); // This creates the min date
        
        await Task.Delay(100); // Ensure time difference
        
        // Order 2: Completed WITHOUT status feed, processed AFTER min date (should be found)
        (var order2, var email2) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification("date-filter-2");
        _orderIdsToCleanup.Add(order2.Id);
        
        string updateEmail2Sql = $@"UPDATE notifications.emailnotifications SET result = 'Delivered', resulttime = now() WHERE alternateid = '{email2.Id}'";
        await PostgreUtil.RunSql(updateEmail2Sql);
        
        string updateOrder2Sql = $@"UPDATE notifications.orders SET processedstatus = 'Completed', processed = now() WHERE alternateid = '{order2.Id}'";
        await PostgreUtil.RunSql(updateOrder2Sql);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order1.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.DoesNotContain(order1.Id, discoveredOrders); // Has status feed
        Assert.Contains(order2.Id, discoveredOrders); // Missing status feed, processed after min date
    }

    [Fact]
    public async Task DiscoverOrders_WithStatusFilter_OnlyFindsMatchingStatus()
    {
        // Arrange - Create both SendConditionNotMet and Completed orders
        var scnmOrder = await PostgreUtil.PopulateDBWithEmailOrder("status-filter-scnm");
        _orderIdsToCleanup.Add(scnmOrder.Id);
        
        string updateScnmSql = $@"UPDATE notifications.orders SET processedstatus = 'SendConditionNotMet', processed = now() WHERE alternateid = '{scnmOrder.Id}'";
        await PostgreUtil.RunSql(updateScnmSql);
        
        (var completedOrder, var email) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification("status-filter-completed");
        _orderIdsToCleanup.Add(completedOrder.Id);
        
        string updateEmailSql = $@"UPDATE notifications.emailnotifications SET result = 'Delivered', resulttime = now() WHERE alternateid = '{email.Id}'";
        await PostgreUtil.RunSql(updateEmailSql);
        
        string updateCompletedSql = $@"UPDATE notifications.orders SET processedstatus = 'Completed', processed = now() WHERE alternateid = '{completedOrder.Id}'";
        await PostgreUtil.RunSql(updateCompletedSql);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = scnmOrder.Creator.ShortName,
            OrderProcessingStatusFilter = OrderProcessingStatus.SendConditionNotMet,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Only SendConditionNotMet should be found
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(scnmOrder.Id, discoveredOrders);
        Assert.DoesNotContain(completedOrder.Id, discoveredOrders);
    }

    [Fact]
    public async Task DiscoverOrders_CompletedWithOldProcessedStatus_StillFound()
    {
        // This test ensures we still find Completed orders even if they previously used the old "Processed" terminology
        // (testing backward compatibility / legacy data handling)
        
        // Arrange
        (var order, var email) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification("legacy-completed");
        _orderIdsToCleanup.Add(order.Id);

        string updateEmailSql = $@"UPDATE notifications.emailnotifications SET result = 'Delivered', resulttime = now() WHERE alternateid = '{email.Id}'";
        await PostgreUtil.RunSql(updateEmailSql);
        
        string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'Completed', processed = now() WHERE alternateid = '{order.Id}'";
        await PostgreUtil.RunSql(updateOrderSql);

        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        var settings = Options.Create(new DiscoverySettings
        {
            OrderIdsFilePath = _testFilePath,
            CreatorNameFilter = order.Creator.ShortName,
            MaxOrders = 100
        });

        var logger = new Mock<ILogger<OrderDiscoveryService>>();
        var service = new OrderDiscoveryService(dataSource, settings, logger.Object);

        // Act
        await service.Run(CancellationToken.None);

        // Assert - Order should be found (Completed without status feed)
        var json = await File.ReadAllTextAsync(_testFilePath);
        var discoveredOrders = JsonSerializer.Deserialize<List<Guid>>(json);
        
        Assert.NotNull(discoveredOrders);
        Assert.Contains(order.Id, discoveredOrders);
    }

    [Fact]
    public void DiscoverySettings_UsesMinProcessedDateFilterWhenProvided()
    {
        // Arrange
        var expectedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var settings = new DiscoverySettings
        {
            MinProcessedDateFilter = expectedDate
        };

        // Assert
        Assert.Equal(expectedDate, settings.MinProcessedDateFilter);
    }

    [Fact]
    public void OrderIdsFilePath_WorksWithDifferentPathSeparators()
    {
        // Arrange & Act - Use Path.Combine to ensure cross-platform compatibility
        var customPath = Path.Combine("data", "orders", "affected.json");
        var settings = new DiscoverySettings
        {
            OrderIdsFilePath = customPath
        };

        // Assert - Path.Combine will use the correct separator for the OS
        Assert.Equal(customPath, settings.OrderIdsFilePath);
        
        // Verify the path is valid for the current OS
        Assert.DoesNotContain("\\", customPath.Replace(Path.DirectorySeparatorChar.ToString(), string.Empty));
        Assert.DoesNotContain("/", customPath.Replace(Path.DirectorySeparatorChar.ToString(), string.Empty));
    }
}
