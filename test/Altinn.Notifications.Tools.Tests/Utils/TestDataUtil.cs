using System;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Npgsql;

namespace Altinn.Notifications.Tools.Tests.Utils;

/// <summary>
/// Minimal test data utility for creating orders and notifications using repositories.
/// Uses the same approach as IntegrationTests for reliable data creation.
/// </summary>
public static class TestDataUtil
{
    /// <summary>
    /// Creates a simple email order with a notification using repository
    /// </summary>
    public static async Task<(Guid OrderId, Guid EmailId)> CreateEmailOrder(string? sendersReference = null)
    {
        sendersReference ??= Guid.NewGuid().ToString();

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
            Id = Guid.NewGuid() // Override ID after creation
        };

        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        var emailRepo = TestServiceUtil.GetService<IEmailNotificationRepository>();
        
        // Create order using repository (ensures proper transaction handling)
        await orderRepo.Create(order);
        
        // Set to Processing status to trigger notification creation
        await orderRepo.SetProcessingStatus(order.Id, OrderProcessingStatus.Processing);
        
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
            Id = Guid.NewGuid() // Override ID
        };

        await emailRepo.AddNotification(emailNotification, DateTime.UtcNow.AddDays(7));
        
        return (order.Id, emailNotification.Id);
    }

    /// <summary>
    /// Creates a simple SMS order using repository
    /// </summary>
    public static async Task<Guid> CreateSmsOrder(string? sendersReference = null)
    {
        sendersReference ??= Guid.NewGuid().ToString();

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
            Id = Guid.NewGuid() // Override ID after creation
        };

        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        
        // Create order using repository (ensures proper transaction handling)
        await orderRepo.Create(order);
        
        return order.Id;
    }

    /// <summary>
    /// Updates order processing status using repository (triggers automatic statusfeed creation when completed)
    /// </summary>
    public static async Task UpdateOrderStatus(Guid orderId, OrderProcessingStatus status)
    {
        var orderRepo = TestServiceUtil.GetService<IOrderRepository>();
        await orderRepo.SetProcessingStatus(orderId, status);
    }

    /// <summary>
    /// Updates order processing status using raw SQL to simulate legacy scenario.
    /// Does NOT trigger automatic statusfeed creation - use this for backfill testing!
    /// </summary>
    public static async Task UpdateOrderStatusLegacy(Guid orderId, OrderProcessingStatus status)
    {
        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();
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

    /// <summary>
    /// Updates email notification result using repository (may trigger automatic statusfeed creation)
    /// </summary>
    public static async Task UpdateEmailNotificationResult(Guid emailId, EmailNotificationResultType resultType)
    {
        var emailRepo = TestServiceUtil.GetService<IEmailNotificationRepository>();
        await emailRepo.UpdateSendStatus(emailId, resultType);
    }

    /// <summary>
    /// Gets the count of status feed entries for an order
    /// </summary>
    public static async Task<int> GetStatusFeedEntryCount(Guid orderId)
    {
        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        
        command.CommandText = @"
            SELECT COUNT(*) 
            FROM notifications.statusfeed sf
            JOIN notifications.orders o ON sf.orderid = o._id
            WHERE o.alternateid = @orderId";
        command.Parameters.AddWithValue("orderId", orderId);
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Deletes all test data created with "ttd" creator
    /// </summary>
    public static async Task CleanupTestData()
    {
        var dataSource = TestServiceUtil.GetService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();
        
        // Delete in correct order due to foreign keys
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM notifications.statusfeed WHERE creatorname = 'ttd'";
            await command.ExecuteNonQueryAsync();
        }
        
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM notifications.orders WHERE creatorname = 'ttd'";
            await command.ExecuteNonQueryAsync();
        }
    }
}
