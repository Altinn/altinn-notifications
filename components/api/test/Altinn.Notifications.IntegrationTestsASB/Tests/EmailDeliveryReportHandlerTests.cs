using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailDeliveryReportHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    [Fact]
    public async Task EmailDeliveryReport_WhenNotificationExists_UpdatesStatusToDelivered()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - Create notification and set status to Succeeded with an operationId
            // (simulates the email service having successfully sent via ACS)
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            string operationId = Guid.NewGuid().ToString();
            await PostgreUtil.UpdateSendStatus(factory, notification.Id, EmailNotificationResultType.Succeeded, operationId);

            // Act - Send a raw EventGrid delivery report to the queue (simulates ACS + Event Grid)
            string queueName = factory.WolverineSettings!.EmailDeliveryReportQueueName;
            await SendDeliveryReportAsync(queueName, operationId, "Delivered");

            // Assert - Poll the database until the handler updates the status to "Delivered"
            var statusUpdated = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var result = await PostgreUtil.RunSqlReturnOutput<string>(
                        _fixture.PostgresConnectionString,
                        "SELECT result FROM notifications.emailnotifications WHERE alternateid = $1",
                        new NpgsqlParameter { Value = notification.Id });
                    return result == EmailNotificationResultType.Delivered.ToString();
                },
                maxAttempts: 20,
                delayMs: 500);
            Assert.True(statusUpdated, "Notification status should be updated to 'Delivered'");

            // Assert - Verify operationId is preserved
            var actualOperationId = await PostgreUtil.RunSqlReturnOutput<string>(
                _fixture.PostgresConnectionString,
                "SELECT operationid FROM notifications.emailnotifications WHERE alternateid = $1",
                new NpgsqlParameter { Value = notification.Id });
            Assert.Equal(operationId, actualOperationId);
        }
    }

    [Fact]
    public async Task EmailDeliveryReport_WhenNotificationNotFound_RetriesAndSavesDeadDeliveryReport()
    {
        // Arrange - Capture logs to count handler attempts via NotificationNotFoundException
        var logCapture = new LogCapture(nameof(NotificationNotFoundException));
        string unmatchedOperationId = Guid.NewGuid().ToString();

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(logCapture))
            .Initialize();

        await using (factory)
        {
            // ACS report arrives before the email service has persisted the operationId
            string queueName = factory.WolverineSettings!.EmailDeliveryReportQueueName;

            // Act - Send delivery report with an operationId that doesn't match any notification
            await SendDeliveryReportAsync(queueName, unmatchedOperationId, "Delivered");

            // Assert - Poll the dead delivery reports table until the report appears after retries exhaust
            // maxAttempts is higher here to account for the full retry chain (cooldown + scheduled retries)
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetDeadDeliveryReportByMessageId(
                         _fixture.PostgresConnectionString,
                         unmatchedOperationId);
                    return deadReport is not null;
                },
                maxAttempts: 40,
                delayMs: 500);
            Assert.True(deadReportFound, "Dead delivery report should be saved after retries are exhausted");
            Assert.Equal("RETRY_THRESHOLD_EXCEEDED", deadReport!.Reason);
            Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, deadReport.Channel);
            Assert.Equal(9, deadReport.AttemptCount);
            Assert.False(deadReport.Resolved);

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

    [Fact]
    public async Task EmailDeliveryReport_WhenNotificationExpired_SavesDeadDeliveryReportWithoutRetry()
    {
        // Arrange - Capture logs to verify the handler only runs once (no retries for expired)
        var logCapture = new LogCapture(nameof(NotificationExpiredException));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(logCapture))
            .Initialize();

        await using (factory)
        {
            // Arrange - Create notification with a future expiry first, set operationId,
            // then expire it. The v2 SQL function blocks updates on expired notifications,
            // so we must set the operationId before expiring — simulating the real scenario
            // where ACS sent the email (setting operationId) before the TTL elapsed.
            string operationId = Guid.NewGuid().ToString();
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            await PostgreUtil.UpdateSendStatus(factory, notification.Id, EmailNotificationResultType.Succeeded, operationId);

            // Now expire the notification by backdating its expiry time
            await PostgreUtil.RunSql(
                _fixture.PostgresConnectionString,
                "UPDATE notifications.emailnotifications SET expirytime = now() - interval '1 minute' WHERE alternateid = $1",
                new NpgsqlParameter { Value = notification.Id });

            string queueName = factory.WolverineSettings!.EmailDeliveryReportQueueName;

            // Act - Send delivery report for the expired notification
            await SendDeliveryReportAsync(queueName, operationId, "Delivered");

            // Assert - Poll the dead delivery reports table until the report appears
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetDeadDeliveryReportByMessageId(
                        _fixture.PostgresConnectionString,
                        operationId);
                    return deadReport is not null;
                },
                maxAttempts: 20,
                delayMs: 500);
            Assert.True(deadReportFound, "Dead delivery report should be saved for expired notifications");
            Assert.Equal("NOTIFICATION_EXPIRED", deadReport!.Reason);
            Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, deadReport.Channel);
            Assert.Equal(1, deadReport.AttemptCount);
            Assert.False(deadReport.Resolved);

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

    [Fact]
    public async Task EmailDeliveryReport_WhenDatabaseThrowsNpgsqlException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange - Create mock service that simulates database errors
        int attemptCount = 0;
        var mockService = new Mock<IEmailNotificationService>();
        mockService.Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Callback<EmailSendOperationResult>(_ => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new NpgsqlException("Simulated database error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailDeliveryReportQueueName;

            // Act - Send delivery report that will trigger NpgsqlException on every attempt
            await SendDeliveryReportAsync(queueName, Guid.NewGuid().ToString(), "Delivered");

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

    private async Task SendDeliveryReportAsync(string queueName, string operationId, string status)
    {
        var eventGridEvent = new
        {
            id = Guid.NewGuid().ToString(),
            topic = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Communication/communicationServices/test",
            subject = $"sender/DoNotReply@altinn.no/message/{operationId}",
            data = new
            {
                sender = "DoNotReply@altinn.no",
                recipient = "recipient@example.com",
                messageId = operationId,
                status,
                deliveryStatusDetails = new { statusMessage = "OK" },
                deliveryAttemptTimeStamp = DateTime.UtcNow.ToString("o")
            },
            eventType = "Microsoft.Communication.EmailDeliveryReportReceived",
            dataVersion = "1.0",
            metadataVersion = "1",
            eventTime = DateTime.UtcNow.ToString("o")
        };

        string body = JsonSerializer.Serialize(eventGridEvent);

        await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
        await using var sender = client.CreateSender(queueName);
        await sender.SendMessageAsync(new ServiceBusMessage(body));
    }
}
