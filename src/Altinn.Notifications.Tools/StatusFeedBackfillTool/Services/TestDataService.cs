using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Npgsql;

namespace StatusFeedBackfillTool.Services;

/// <summary>
/// Service for generating and cleaning up test data for manual testing.
/// Creates diverse test scenarios to verify the backfill tool's discovery and insertion logic.
/// </summary>
public class TestDataService(
    IOrderRepository orderRepository,
    IEmailNotificationRepository emailRepository,
    NpgsqlDataSource dataSource)
{
    private const string _testDataPrefix = "backfill-tool-test-";
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IEmailNotificationRepository _emailRepository = emailRepository;
    private readonly NpgsqlDataSource _dataSource = dataSource;


    /// <summary>
    /// Creates diverse test orders to verify discovery logic:
    /// - 2 SendConditionNotMet WITHOUT status feed (SHOULD be found)
    /// - 1 SendConditionNotMet WITH status feed entry (should NOT be found - already has entry)
    /// - 2 Processing WITHOUT status feed (should NOT be found - not final order status)
    ///   * Different email results: "Sending", "New"
    /// - 3 Completed WITH status feed entries (should NOT be found - already have entries)
    /// - 2 Completed WITHOUT status feed (SHOULD be found)
    /// </summary>
    public async Task GenerateTestData()
    {
        var ordersToFind = new List<Guid>();
        var ordersNotToFind = new List<Guid>();

        Console.WriteLine("\nGenerating test data...\n");

        // Create orders WITH status feed first to establish minimum statusfeed date
        await CreateSendConditionNotMetWithStatusFeed(ordersNotToFind, count: 1);
        await CreateCompletedWithStatusFeed(ordersNotToFind, count: 3);
        await CreateProcessingWithoutStatusFeed(ordersNotToFind, count: 2);
        
        // Then create orders WITHOUT status feed - their processed timestamps will be after the min date
        await CreateSendConditionNotMetWithoutStatusFeed(ordersToFind, count: 2);
        await CreateCompletedWithoutStatusFeed(ordersToFind, count: 2);

        PrintTestDataSummary(ordersToFind, ordersNotToFind);
    }

    /// <summary>
    /// Cleanup all test data created by this tool (identified by sender reference prefix)
    /// </summary>
    public async Task CleanupTestData(CancellationToken cancellationToken)
    {
        Console.WriteLine("\nCleaning up test data...\n");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        
        // Delete in correct order due to foreign keys
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                DELETE FROM notifications.statusfeed 
                WHERE orderid IN (
                    SELECT _id FROM notifications.orders 
                    WHERE sendersreference LIKE @prefix
                )";
            command.Parameters.AddWithValue("prefix", _testDataPrefix + "%");
            var deletedStatusFeed = await command.ExecuteNonQueryAsync(cancellationToken);
            Console.WriteLine($"Deleted {deletedStatusFeed} status feed entries");
        }
        
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM notifications.orders WHERE sendersreference LIKE @prefix";
            command.Parameters.AddWithValue("prefix", _testDataPrefix + "%");
            var deletedOrders = await command.ExecuteNonQueryAsync(cancellationToken);
            Console.WriteLine($"Deleted {deletedOrders} test orders");
        }

        Console.WriteLine("\n========================================");
        Console.WriteLine("Manual test data cleaned up successfully!");
        Console.WriteLine("========================================\n");
    }

    private async Task CreateSendConditionNotMetWithoutStatusFeed(List<Guid> ordersToFind, int count)
    {
        // === SendConditionNotMet orders WITHOUT status feed (SHOULD BE FOUND) ===
        // SendConditionNotMet happens BEFORE notifications are created, so no email/SMS records
        for (int i = 0; i < count; i++)
        {
            var orderId = await CreateSmsOrder($"{_testDataPrefix}scnm-without-sf-{i}");
            ordersToFind.Add(orderId);

            // Use legacy update to avoid automatic status feed creation
            await UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.SendConditionNotMet);
        }
    }

    private async Task CreateSendConditionNotMetWithStatusFeed(List<Guid> ordersNotToFind, int count)
    {
        // === SendConditionNotMet WITH status feed entry (should NOT be found) ===
        // SendConditionNotMet happens BEFORE notifications are created, so no email/SMS records
        for (int i = 0; i < count; i++)
        {
            var orderId = await CreateSmsOrder($"{_testDataPrefix}scnm-with-sf-{i}");
            ordersNotToFind.Add(orderId);
            
            // Update order status to SendConditionNotMet
            await _orderRepository.SetProcessingStatus(orderId, OrderProcessingStatus.SendConditionNotMet);
            
            // Manually create status feed entry using the order repository method
            await _orderRepository.InsertStatusFeedForOrder(orderId);
        }
    }

    private async Task CreateProcessingWithoutStatusFeed(List<Guid> ordersNotToFind, int count)
    {
        // === Processing orders WITHOUT status feed (should NOT be found - not final) ===
        for (int i = 0; i < count; i++)
        {
            var (orderId, emailId) = await CreateEmailOrder($"{_testDataPrefix}processing-{i}");
            ordersNotToFind.Add(orderId);
            
            // Keep order in Processing state
            await _orderRepository.SetProcessingStatus(orderId, OrderProcessingStatus.Processing);
            
            // Vary email results to show different processing states
            EmailNotificationResultType emailResult = i % 2 == 0 
                ? EmailNotificationResultType.Sending 
                : EmailNotificationResultType.New;
            await _emailRepository.UpdateSendStatus(emailId, emailResult);
        }
    }

    private async Task CreateCompletedWithStatusFeed(List<Guid> ordersNotToFind, int count)
    {
        // === Completed orders WITH status feed entries (should NOT be found) ===
        for (int i = 0; i < count; i++)
        {
            var (orderId, emailId) = await CreateEmailOrder($"{_testDataPrefix}completed-with-sf-{i}");
            ordersNotToFind.Add(orderId);
            
            // Update order and email statuses
            await _orderRepository.SetProcessingStatus(orderId, OrderProcessingStatus.Completed);
            
            // Set different email results to test variety
            EmailNotificationResultType emailStatus = i switch
            {
                0 => EmailNotificationResultType.Delivered,
                1 => EmailNotificationResultType.Failed,
                _ => EmailNotificationResultType.Failed_Bounced
            };
            await _emailRepository.UpdateSendStatus(emailId, emailStatus);
            
            // Manually create status feed entry using the order repository method
            await _orderRepository.InsertStatusFeedForOrder(orderId);
        }
    }

    private async Task CreateCompletedWithoutStatusFeed(List<Guid> ordersToFind, int count)
    {
        // === Completed orders WITHOUT status feed (SHOULD BE FOUND) ===
        // This simulates legacy orders that were completed before automatic status feed creation
        for (int i = 0; i < count; i++)
        {
            var (orderId, _) = await CreateEmailOrder($"{_testDataPrefix}completed-without-sf-{i}");
            ordersToFind.Add(orderId);

            // Use legacy update to simulate old orders completed without automatic statusfeed creation
            await UpdateOrderStatusLegacy(orderId, OrderProcessingStatus.Completed);
        }
    }

    private async Task<(Guid OrderId, Guid EmailId)> CreateEmailOrder(string sendersReference)
    {
        var order = new NotificationOrder
        {
            SendersReference = sendersReference,
            Templates =
            [
                new EmailTemplate()
                {
                    FromAddress = "noreply@altinn.no",
                    Subject = "Test notification",
                    Body = "This is a test notification",
                    ContentType = EmailContentType.Plain
                }
            ],
            RequestedSendTime = DateTime.UtcNow,
            NotificationChannel = NotificationChannel.Email,
            Creator = new("ttd"),
            Created = DateTime.UtcNow,
            Recipients =
            [
                new Recipient()
                {
                    AddressInfo =
                    [
                        new EmailAddressPoint()
                        {
                            AddressType = AddressType.Email,
                            EmailAddress = "test@example.com"
                        }
                    ]
                }
            ],
            Id = Guid.NewGuid()
        };

        // Create order using repository
        await _orderRepository.Create(order);
        
        // Set to Processing status to trigger notification creation
        await _orderRepository.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
        
        // Create email notification
        var recipient = order.Recipients[0];
        var addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        var emailNotification = new EmailNotification(order.Id, order.RequestedSendTime)
        {
            Recipient = new()
            {
                ToAddress = addressPoint!.EmailAddress
            },
            SendResult = new(EmailNotificationResultType.New, DateTime.UtcNow),
            Id = Guid.NewGuid()
        };

        await _emailRepository.AddNotification(emailNotification, DateTime.UtcNow.AddDays(7));
        
        return (order.Id, emailNotification.Id);
    }

    private async Task<Guid> CreateSmsOrder(string sendersReference)
    {
        var order = new NotificationOrder
        {
            SendersReference = sendersReference,
            Templates =
            [
                new SmsTemplate()
                {
                    Body = "Test SMS notification",
                    SenderNumber = "Altinn"
                }
            ],
            RequestedSendTime = DateTime.UtcNow,
            NotificationChannel = NotificationChannel.Sms,
            Creator = new("ttd"),
            Created = DateTime.UtcNow,
            Recipients =
            [
                new Recipient()
                {
                    AddressInfo =
                    [
                        new SmsAddressPoint()
                        {
                            AddressType = AddressType.Sms,
                            MobileNumber = "+4712345678"
                        }
                    ]
                }
            ],
            Id = Guid.NewGuid()
        };

        // Create order using repository
        await _orderRepository.Create(order);
        
        return order.Id;
    }

    /// <summary>
    /// Updates order processing status using raw SQL to simulate legacy scenario.
    /// Does NOT trigger automatic statusfeed creation - use this for backfill testing!
    /// </summary>
    private async Task UpdateOrderStatusLegacy(Guid orderId, OrderProcessingStatus status)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        
        command.CommandText = @"
            UPDATE notifications.orders 
            SET processedstatus = @status::orderprocessingstate,
                processed = @processed
            WHERE alternateid = @orderId";
        
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("processed", DateTime.UtcNow);
        command.Parameters.AddWithValue("orderId", orderId);
        
        await command.ExecuteNonQueryAsync();
    }

    private static void PrintTestDataSummary(List<Guid> ordersToFind, List<Guid> ordersNotToFind)
    {
        // Print the order IDs and instructions
        Console.WriteLine("\n========================================");
        Console.WriteLine("TEST DATA CREATED SUCCESSFULLY");
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
        Console.WriteLine("\nTotal: 10 test orders (4 should be found, 6 should be skipped)");
        Console.WriteLine("\nYou can now run option 1 (Discover) to verify the tool finds the correct orders.");
        Console.WriteLine("========================================\n");
    }
}
