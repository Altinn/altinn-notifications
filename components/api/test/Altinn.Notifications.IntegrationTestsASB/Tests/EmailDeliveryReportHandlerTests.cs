using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.IntegrationTestsASB.Extensions;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for the email delivery report Wolverine handler.
/// Sends raw EventGrid-formatted messages to the ASB queue (simulating ACS + Event Grid)
/// and verifies end-to-end processing through the handler pipeline.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailDeliveryReportHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the happy path: a delivery report with status "Delivered" is received
    /// for a notification that exists in the database with a matching operationId.
    /// The handler should update the notification status to "Delivered".
    /// </summary>
    [Fact]
    public async Task EmailDeliveryReport_WhenNotificationExists_UpdatesStatusToDelivered()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - Create notification and set status to Succeeded with an operationId
            // (simulates the email service having successfully sent via ACS)
            var (order, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            string operationId = Guid.NewGuid().ToString();
            await PostgreUtil.UpdateSendStatus(factory, notification.Id, EmailNotificationResultType.Succeeded, operationId);

            // Act - Send a raw EventGrid delivery report to the queue (simulates ACS + Event Grid)
            string queueName = factory.WolverineSettings.EmailDeliveryReportQueueName;
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

    /// <summary>
    /// Tests that when the delivery report's operationId doesn't match any notification in the database,
    /// the handler retries according to the configured policy and eventually saves a dead delivery report.
    /// Simulates the realistic scenario where ACS delivers a report before the notification
    /// has been persisted with its operationId.
    /// </summary>
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
            string queueName = factory.WolverineSettings.EmailDeliveryReportQueueName;

            // Act - Send delivery report with an operationId that doesn't match any notification
            await SendDeliveryReportAsync(queueName, unmatchedOperationId, "Delivered");

            // Assert - Poll the dead delivery reports table until the report appears after retries exhaust
            // maxAttempts is higher here to account for the full retry chain (cooldown + scheduled retries)
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var count = await PostgreUtil.RunSqlReturnOutput<long>(
                        _fixture.PostgresConnectionString,
                        "SELECT count(1) FROM notifications.deaddeliveryreports");
                    return count > 0;
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
    /// The expiry is triggered naturally by the SQL function when expirytime &lt;= now().
    /// </summary>
    [Fact]
    public async Task EmailDeliveryReport_WhenNotificationExpired_SavesDeadDeliveryReportWithoutRetry()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - Create notification with a future expiry first, set operationId,
            // then expire it. The v2 SQL function blocks updates on expired notifications,
            // so we must set the operationId before expiring — simulating the real scenario
            // where ACS sent the email (setting operationId) before the TTL elapsed.
            string operationId = Guid.NewGuid().ToString();
            var (order, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            await PostgreUtil.UpdateSendStatus(factory, notification.Id, EmailNotificationResultType.Succeeded, operationId);

            // Now expire the notification by backdating its expiry time
            await PostgreUtil.RunSql(
                _fixture.PostgresConnectionString,
                "UPDATE notifications.emailnotifications SET expirytime = now() - interval '1 minute' WHERE alternateid = $1",
                new NpgsqlParameter { Value = notification.Id });

            string queueName = factory.WolverineSettings.EmailDeliveryReportQueueName;

            // Act - Send delivery report for the expired notification
            await SendDeliveryReportAsync(queueName, operationId, "Delivered");

            // Assert - Poll the dead delivery reports table until the report appears
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    var count = await PostgreUtil.RunSqlReturnOutput<long>(
                        _fixture.PostgresConnectionString,
                        "SELECT count(1) FROM notifications.deaddeliveryreports");
                    return count > 0;
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
        }
    }

    /// <summary>
    /// Sends a raw EventGrid-formatted delivery report message to the specified ASB queue,
    /// simulating what Azure Communication Services + Event Grid would produce.
    /// </summary>
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
