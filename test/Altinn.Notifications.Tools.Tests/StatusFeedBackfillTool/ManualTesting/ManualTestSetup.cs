using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Tools.Tests.Utils;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.StatusFeedBackfillTool.ManualTesting;

/// <summary>
/// Manual test setup class - creates test data WITHOUT cleanup.
/// Use this to set up scenarios for manual tool testing.
/// </summary>
public class ManualTestSetup
{
    // Set to true to enable these tests for manual execution
    private const bool _enableManualTests = false;
    
    /// <summary>
    /// Creates diverse test orders to verify discovery logic:
    /// - 2 SendConditionNotMet WITHOUT status feed (SHOULD be found)
    /// - 1 SendConditionNotMet WITH status feed entry (should NOT be found - already has entry)
    /// - 2 Processing WITHOUT status feed (should NOT be found - not final order status)
    ///   * Different email results: "Sending", "New"
    /// - 3 Completed WITH status feed entries (should NOT be found - already have entries)
    /// - 2 Completed WITHOUT status feed (SHOULD be found)
    /// </summary>
    [Fact(Skip = _enableManualTests ? null : "Manual test - set _enableManualTests = true to run")]
    public async Task CreateTestOrders_WithoutStatusFeed_ForManualTesting()
    {
        var ordersToFind = new List<Guid>();
        var ordersNotToFind = new List<Guid>();

        // Create orders WITH status feed first to establish minimum statusfeed date
        await CreateSendConditionNotMetWithStatusFeed(ordersNotToFind, count: 1);
        await CreateCompletedWithStatusFeed(ordersNotToFind, count: 3);
        await CreateProcessingWithoutStatusFeed(ordersNotToFind, count: 2);
        
        // Then create orders WITHOUT status feed - their processed timestamps will be after the min date
        await CreateSendConditionNotMetWithoutStatusFeed(ordersToFind, count: 2);
        await CreateCompletedWithoutStatusFeed(ordersToFind, count: 2);

        PrintTestDataSummary(ordersToFind, ordersNotToFind);

        // Verify we have the right counts
        Assert.Equal(4, ordersToFind.Count);
        Assert.Equal(6, ordersNotToFind.Count);
    }

    /// <summary>
    /// Cleanup helper - manually remove all test data created by manual testing.
    /// Run this when you're done with manual testing.
    /// </summary>
    [Fact(Skip = _enableManualTests ? null : "Manual test - set _enableManualTests = true to run")]
    public async Task Cleanup_ManualTestData()
    {
        string deleteStatusFeedSql = @"DELETE FROM notifications.statusfeed WHERE creatorname = 'ttd'";
        await PostgreUtil.RunSql(deleteStatusFeedSql);

        string deleteOrdersSql = @"DELETE FROM notifications.orders WHERE creatorname = 'ttd'";
        await PostgreUtil.RunSql(deleteOrdersSql);

        Console.WriteLine("\n========================================");
        Console.WriteLine("Manual test data cleaned up successfully!");
        Console.WriteLine("========================================\n");

        Assert.True(true);
    }

    private static async Task CreateSendConditionNotMetWithoutStatusFeed(List<Guid> ordersToFind, int count)
    {
        // === SendConditionNotMet orders WITHOUT status feed (SHOULD BE FOUND) ===
        // SendConditionNotMet happens BEFORE notifications are created, so no email/SMS records
        for (int i = 0; i < count; i++)
        {
            var order = await PostgreUtil.PopulateDBWithEmailOrder($"scnm-without-sf-{i}");
            ordersToFind.Add(order.Id);
            
            // Update order to SendConditionNotMet via SQL to avoid automatic status feed creation
            string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'SendConditionNotMet', processed = now() WHERE alternateid = '{order.Id}'";
            await PostgreUtil.RunSql(updateOrderSql);
        }
    }

    private static async Task CreateSendConditionNotMetWithStatusFeed(List<Guid> ordersNotToFind, int count)
    {
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();

        // === SendConditionNotMet WITH status feed entry (should NOT be found) ===
        // SendConditionNotMet happens BEFORE notifications are created, so no email/SMS records
        for (int i = 0; i < count; i++)
        {
            var order = await PostgreUtil.PopulateDBWithEmailOrder($"scnm-with-sf-{i}");
            ordersNotToFind.Add(order.Id);
            
            // Update order status to SendConditionNotMet
            await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.SendConditionNotMet);
            
            // Manually create status feed entry using the order repository method
            await orderRepo.InsertStatusFeedForOrder(order.Id);
        }
    }

    private static async Task CreateProcessingWithoutStatusFeed(List<Guid> ordersNotToFind, int count)
    {
        // === Processing orders WITHOUT status feed (should NOT be found - not final) ===
        for (int i = 0; i < count; i++)
        {
            (var order, var email) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"processing-{i}");
            ordersNotToFind.Add(order.Id);
            
            // Keep order in Processing state with different intermediate email statuses
            string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'Processing' WHERE alternateid = '{order.Id}'";
            await PostgreUtil.RunSql(updateOrderSql);
            
            // Vary email results to show different processing states
            string emailResult = i % 2 == 0 ? "Sending" : "New";
            string updateEmailSql = $@"UPDATE notifications.emailnotifications SET result = '{emailResult}' WHERE alternateid = '{email.Id}'";
            await PostgreUtil.RunSql(updateEmailSql);
        }
    }

    private static async Task CreateCompletedWithStatusFeed(List<Guid> ordersNotToFind, int count)
    {
        var emailRepo = TestServiceUtil.GetService<IEmailNotificationRepository>();
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();

        // === Completed orders WITH status feed entries (should NOT be found) ===
        for (int i = 0; i < count; i++)
        {
            (var order, var email) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"completed-with-sf-{i}");
            ordersNotToFind.Add(order.Id);
            
            // Update order and email statuses
            await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Completed);
            
            // Set different email results to test variety
            EmailNotificationResultType emailStatus = i switch
            {
                0 => EmailNotificationResultType.Delivered,
                1 => EmailNotificationResultType.Failed,
                _ => EmailNotificationResultType.Failed_Bounced
            };
            await emailRepo.UpdateSendStatus(email.Id, emailStatus);
            
            // Manually create status feed entry using the order repository method
            await orderRepo.InsertStatusFeedForOrder(order.Id);
        }
    }

    private static async Task CreateCompletedWithoutStatusFeed(List<Guid> ordersToFind, int count)
    {
        // === Completed orders WITHOUT status feed (SHOULD BE FOUND) ===
        for (int i = 0; i < count; i++)
        {
            (var order, var email) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification($"completed-without-sf-{i}");
            ordersToFind.Add(order.Id);
            
            // Update via SQL to avoid automatic status feed creation
            string updateEmailSql = $@"UPDATE notifications.emailnotifications SET result = 'Delivered', resulttime = now() WHERE alternateid = '{email.Id}'";
            await PostgreUtil.RunSql(updateEmailSql);
            
            string updateOrderSql = $@"UPDATE notifications.orders SET processedstatus = 'Completed', processed = now() WHERE alternateid = '{order.Id}'";
            await PostgreUtil.RunSql(updateOrderSql);
        }
    }

    private static void PrintTestDataSummary(List<Guid> ordersToFind, List<Guid> ordersNotToFind)
    {
        // Print the order IDs and instructions
        Console.WriteLine("\n========================================");
        Console.WriteLine("TEST DATA CREATED (NOT CLEANED UP)");
        Console.WriteLine("========================================\n");
        Console.WriteLine($"SHOULD BE FOUND ({ordersToFind.Count} orders - missing status feed):");
        foreach (var orderId in ordersToFind)
        {
            Console.WriteLine($"  - {orderId}");
        }
        
        Console.WriteLine($"\nshould NOT be found ({ordersNotToFind.Count} orders - have entries or non-final status):");
        foreach (var orderId in ordersNotToFind)
        {
            Console.WriteLine($"  - {orderId}");
        }

        Console.WriteLine("\nTest scenario:");
        Console.WriteLine("- SendConditionNotMet + final emails without status feed = FIND");
        Console.WriteLine("- Completed + final emails without status feed = FIND");
        Console.WriteLine("- SendConditionNotMet/Completed WITH status feed = SKIP (already has entry)");
        Console.WriteLine("- Processing with intermediate email results (Sending/New) = SKIP (not eligible for backfill)");
    }
}
