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

[Collection(nameof(IntegrationTestContainersCollection))]
public class SmsDeliveryReportHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

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
            string queueName = factory.WolverineSettings!.SmsDeliveryReportQueueName;
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
            var policy = factory.WolverineSettings!.SmsDeliveryReportQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
            string queueName = factory.WolverineSettings.SmsDeliveryReportQueueName;

            // Act - Send delivery report with a gatewayReference that doesn't match any notification
            await factory.SendToQueueAsync(queueName, new SmsDeliveryReportCommand
            {
                NotificationId = null,
                GatewayReference = unmatchedGatewayReference,
                SendResult = "Delivered"
            });

            // Assert - Poll the dead delivery reports table until the report appears after retries exhaust
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetDeadDeliveryReportByGatewayReference(
                         _fixture.PostgresConnectionString,
                         unmatchedGatewayReference);
                    return deadReport is not null;
                },
                maxAttempts: 40,
                delayMs: 500);
            Assert.True(deadReportFound, "Dead delivery report should be saved after retries are exhausted");
            Assert.Equal("RETRY_THRESHOLD_EXCEEDED", deadReport!.Reason);
            Assert.Equal(DeliveryReportChannel.LinkMobility, deadReport.Channel);
            Assert.Equal(expectedAttempts, deadReport.AttemptCount);
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

            // Assert - Verify the handler was called exactly as many times as the policy dictates
            Console.WriteLine($"[Test] NotificationNotFoundException logged {logCapture.Count} times (expected {expectedAttempts})");
            Assert.Equal(expectedAttempts, logCapture.Count);
        }
    }

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
            string queueName = factory.WolverineSettings!.SmsDeliveryReportQueueName;
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
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetDeadDeliveryReportByGatewayReference(
                        _fixture.PostgresConnectionString,
                        gatewayReference);
                    return deadReport is not null;
                },
                maxAttempts: 20,
                delayMs: 500);
            Assert.True(deadReportFound, "Dead delivery report should be saved for expired notifications");
            Assert.Equal("NOTIFICATION_EXPIRED", deadReport!.Reason);
            Assert.Equal(DeliveryReportChannel.LinkMobility, deadReport.Channel);
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
            var policy = factory.WolverineSettings!.SmsDeliveryReportQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
            string queueName = factory.WolverineSettings.SmsDeliveryReportQueueName;

            string gatewayReference = Guid.NewGuid().ToString();

            // Act - Send delivery report that will trigger NpgsqlException on every attempt
            await factory.SendToQueueAsync(queueName, new SmsDeliveryReportCommand
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = gatewayReference,
                SendResult = "Delivered"
            });

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(30));
            Assert.NotNull(deadLetterMessage);

            // Assert - Dead delivery reports table should be empty (NpgsqlException goes to DLQ, not dead reports)
            var deadReportId = await PostgreUtil.GetDeadDeliveryReportByGatewayReference(
                _fixture.PostgresConnectionString,
                gatewayReference);
            Assert.Null(deadReportId);

            // Assert - Verify the handler was called exactly as many times as the policy dictates
            Console.WriteLine($"[Test] Handler was called {attemptCount} times (expected {expectedAttempts})");
            Assert.Equal(expectedAttempts, attemptCount);
        }
    }

    [Fact]
    public async Task SmsDeliveryReport_WhenSendResultIsInvalid_GoesToDeadLetterQueueWithoutRetry()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.SmsDeliveryReportQueueName;
            string gatewayReference = Guid.NewGuid().ToString();

            // Act - Send delivery report with a SendResult value that is not a valid enum member.
            // Enum.Parse<SmsNotificationResultType> will throw ArgumentException, which is not
            // in any handler chain and should go straight to DLQ with no retries.
            await factory.SendToQueueAsync(queueName, new SmsDeliveryReportCommand
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = gatewayReference,
                SendResult = "NotARealStatus"
            });

            // Assert - Message should appear in DLQ immediately (no retries for ArgumentException)
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(30));
            Assert.NotNull(deadLetterMessage);

            // Assert - No dead delivery report should be created (ArgumentException is not handled by SaveDeadDeliveryReport)
            var deadReport = await PostgreUtil.GetDeadDeliveryReportByGatewayReference(
                _fixture.PostgresConnectionString,
                gatewayReference);
            Assert.Null(deadReport);
        }
    }
}
