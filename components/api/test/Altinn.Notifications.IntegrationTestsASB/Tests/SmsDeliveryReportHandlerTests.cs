using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for the SMS delivery report Wolverine handler.
/// Sends SmsDeliveryReportCommand messages to the ASB queue via Wolverine (simulating the SMS service)
/// and verifies end-to-end processing through the handler pipeline.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SmsDeliveryReportHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the happy path: a delivery report with status "Delivered" is received
    /// for an SMS notification that exists in the database with a matching gatewayReference.
    /// The handler should update the notification status to "Delivered".
    /// </summary>
    [Fact]
    public async Task SmsDeliveryReport_WhenNotificationExists_UpdatesStatusToDelivered()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - Create notification and set status to Accepted with a gatewayReference
            // (simulates the SMS service having successfully sent via Link Mobility)
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(factory);
            string gatewayReference = Guid.NewGuid().ToString();
            string queueName = factory.WolverineSettings.SmsDeliveryReportQueueName;
            await PostgreUtil.UpdateSmsSendStatus(factory, notification.Id, SmsNotificationResultType.Accepted, gatewayReference);

            // Act - Send an SMS delivery report command to the queue via Wolverine (simulates the SMS service)
            await factory.SendToQueueAsync(queueName, new SmsDeliveryReportCommand
            {
                NotificationId = notification.Id,
                GatewayReference = gatewayReference,
                SendResult = "Delivered"
            });

            // Assert - Poll the database until the handler updates the status to "Delivered"
            var statusUpdated = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var result = await PostgreUtil.RunSqlReturnOutput<string>(
                        _fixture.PostgresConnectionString,
                        "SELECT result FROM notifications.smsnotifications WHERE alternateid = $1",
                        new NpgsqlParameter { Value = notification.Id });
                    return result == SmsNotificationResultType.Delivered.ToString();
                },
                maxAttempts: 20,
                delayMs: 500);
            Assert.True(statusUpdated, "Notification status should be updated to 'Delivered'");

            // Assert - Verify gatewayReference is preserved
            var actualRef = await PostgreUtil.RunSqlReturnOutput<string>(
                _fixture.PostgresConnectionString,
                "SELECT gatewayreference FROM notifications.smsnotifications WHERE alternateid = $1",
                new NpgsqlParameter { Value = notification.Id });
            Assert.Equal(gatewayReference, actualRef);
        }
    }

    /// <summary>
    /// Tests that when the delivery report's gatewayReference doesn't match any notification in the database,
    /// the handler retries according to the configured policy and eventually saves a dead delivery report.
    /// </summary>
    [Fact]
    public async Task SmsDeliveryReport_WhenNotificationNotFound_RetriesAndSavesDeadDeliveryReport()
    {
        // Arrange - Capture logs to count handler attempts via NotificationNotFoundException
        var logCapture = new LogCapture(nameof(NotificationNotFoundException));
        string unmatchedGatewayReference = Guid.NewGuid().ToString();

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(logCapture))
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings.SmsDeliveryReportQueueName;

            // Act - Send delivery report with a gatewayReference that doesn't match any notification
            await factory.SendToQueueAsync(queueName, new SmsDeliveryReportCommand
            {
                NotificationId = null,
                GatewayReference = unmatchedGatewayReference,
                SendResult = "Delivered"
            });

            // Assert - Poll the dead delivery reports table until the report appears after retries exhaust
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var id = await PostgreUtil.GetDeadDeliveryReportByGatewayReference(
                         _fixture.PostgresConnectionString,
                         unmatchedGatewayReference);
                    return id.HasValue;
                },
                maxAttempts: 40,
                delayMs: 500);
            Assert.True(deadReportFound, "Dead delivery report should be saved after retries are exhausted");

            // Assert - Queue should be empty (message was handled, not moved to DLQ)
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty — report is saved to dead delivery reports, not DLQ");

            // Assert - DLQ is empty
            var dlqEmpty = await ServiceBusTestUtils.WaitForDeadLetterEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(dlqEmpty, "Dead letter queue should be empty — NotificationNotFoundException should not trigger DLQ");

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
            // ScheduleRetry(500ms, 500ms, 500ms, 500ms, 500ms) = 5 more retries with new locks
            // Total: 1 initial + 3 cooldown retries + 5 scheduled retries = 9 attempts
            Console.WriteLine($"[Test] NotificationNotFoundException logged {logCapture.Count} times");
            Assert.Equal(9, logCapture.Count);
        }
    }

    /// <summary>
    /// Tests that when the notification has expired (past its TTL), the handler catches
    /// the NotificationExpiredException and saves a dead delivery report without retrying.
    /// </summary>
    [Fact]
    public async Task SmsDeliveryReport_WhenNotificationExpired_SavesDeadDeliveryReportWithoutRetry()
    {
        // Arrange - Capture logs to verify the handler only runs once (no retries for expired)
        var logCapture = new LogCapture(nameof(NotificationExpiredException));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(logCapture))
            .Initialize();

        await using (factory)
        {
            // Arrange - Create notification, set gatewayReference via Accepted status,
            // then expire it. Must set gatewayReference before expiring — the SQL function
            // blocks updates on expired notifications.
            string gatewayReference = Guid.NewGuid().ToString();
            string queueName = factory.WolverineSettings.SmsDeliveryReportQueueName;
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(factory);
            await PostgreUtil.UpdateSmsSendStatus(factory, notification.Id, SmsNotificationResultType.Accepted, gatewayReference);

            // Now expire the notification by backdating its expiry time
            await PostgreUtil.RunSql(
                _fixture.PostgresConnectionString,
                "UPDATE notifications.smsnotifications SET expirytime = now() - interval '1 minute' WHERE alternateid = $1",
                new NpgsqlParameter { Value = notification.Id });

            // Act - Send delivery report for the expired notification
            await factory.SendToQueueAsync(queueName, new SmsDeliveryReportCommand
            {
                NotificationId = notification.Id,
                GatewayReference = gatewayReference,
                SendResult = "Delivered"
            });

            // Assert - Poll the dead delivery reports table until the report appears
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var id = await PostgreUtil.GetDeadDeliveryReportByGatewayReference(
                        _fixture.PostgresConnectionString,
                        gatewayReference);
                    return id.HasValue;
                },
                maxAttempts: 20,
                delayMs: 500);
            Assert.True(deadReportFound, "Dead delivery report should be saved for expired notifications");

            // Assert - Queue should be empty (message was handled, no retry)
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty — expired reports are not retried");

            // Assert - Verify the handler encountered the exception exactly once (no retries)
            Console.WriteLine($"[Test] NotificationExpiredException logged {logCapture.Count} times");
            Assert.Equal(1, logCapture.Count);
        }
    }

    /// <summary>
    /// Tests that when the database throws NpgsqlException, the handler retries
    /// according to the configured policy and eventually moves the message to the dead letter queue.
    /// </summary>
    [Fact]
    public async Task SmsDeliveryReport_WhenDatabaseThrowsNpgsqlException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange - Create mock service that simulates database errors
        int attemptCount = 0;
        var mockService = new Mock<ISmsNotificationService>();
        mockService.Setup(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
            .Callback<SmsSendOperationResult>(_ => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new NpgsqlException("Simulated database error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings.SmsDeliveryReportQueueName;

            // Act - Send delivery report that will trigger NpgsqlException on every attempt
            await factory.SendToQueueAsync(queueName, new SmsDeliveryReportCommand
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = Guid.NewGuid().ToString(),
                SendResult = "Delivered"
            });

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(30));
            Assert.NotNull(deadLetterMessage);

            // Assert - Dead delivery reports table should be empty (NpgsqlException goes to DLQ, not dead reports)
            var deadReportCount = await PostgreUtil.RunSqlReturnOutput<long>(
                _fixture.PostgresConnectionString,
                "SELECT count(1) FROM notifications.deaddeliveryreports");
            Assert.Equal(0, deadReportCount);

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
            // ScheduleRetry(500ms, 500ms, 500ms, 500ms, 500ms) = 5 more retries with new locks
            // Total: 1 initial + 3 cooldown retries + 5 scheduled retries = 9 attempts
            Console.WriteLine($"[Test] Handler was called {attemptCount} times");
            Assert.Equal(9, attemptCount);
        }
    }
}
