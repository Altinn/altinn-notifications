using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
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
public class EmailSendResultHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    [Fact]
    public async Task EmailSendResult_WhenNotificationExists_UpdatesStatusToDelivered()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - Create notification and set status to Succeeded with an operationId
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            string operationId = Guid.NewGuid().ToString();
            await PostgreUtil.UpdateEmailSendStatus(factory, notification.Id, EmailNotificationResultType.Succeeded, operationId);

            string queueName = factory.WolverineSettings!.EmailSendResultQueueName;

            var command = new EmailSendResultCommand
            {
                NotificationId = notification.Id,
                OperationId = operationId,
                SendResult = EmailNotificationResultType.Delivered.ToString()
            };

            // Act - Send the command to the ASB queue (simulating the email service)
            await factory.SendToQueueAsync(queueName, command);

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
        }
    }

    [Fact]
    public async Task EmailSendResult_WhenDatabaseThrowsNpgsqlException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange - Create mock service that simulates database errors
        int attemptCount = 0;
        var mockService = new Mock<IEmailNotificationService>();
        mockService
            .Setup(s => s.UpdateSendStatus(It.IsAny<Core.Models.Notification.EmailSendOperationResult>()))
            .Callback<Core.Models.Notification.EmailSendOperationResult>(_ => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new NpgsqlException("Simulated database error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.EmailSendResultQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;

            string queueName = factory.WolverineSettings!.EmailSendResultQueueName;

            var command = new EmailSendResultCommand
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = EmailNotificationResultType.Delivered.ToString()
            };

            // Act - Send a command that will trigger NpgsqlException on every attempt
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var timeout = TimeSpan.FromMilliseconds(policy.CooldownDelaysMs.Sum() + policy.ScheduleDelaysMs.Sum()) + TimeSpan.FromSeconds(10);
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                timeout);
            Assert.NotNull(deadLetterMessage);

            // Assert - Verify the handler was called exactly as many times as the policy dictates
            Console.WriteLine($"[Test] Handler was called {attemptCount} times (expected {expectedAttempts})");
            Assert.Equal(expectedAttempts, attemptCount);
        }
    }

    [Fact]
    public async Task EmailSendResult_WhenSendResultIsUnknown_SavesDeadDeliveryReportAndDiscardsMessage()
    {
        // Arrange - mock service to confirm the handler short-circuits before UpdateEmailSendStatus
        var mockService = new Mock<IEmailNotificationService>(MockBehavior.Strict);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            string operationId = Guid.NewGuid().ToString();
            string queueName = factory.WolverineSettings!.EmailSendResultQueueName;

            var command = new EmailSendResultCommand
            {
                NotificationId = Guid.NewGuid(),
                OperationId = operationId,
                SendResult = "UnknownResultValue_XYZ"
            };

            // Act - Send command with an unrecognized SendResult value
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Dead delivery report should be saved to the database
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetDeadDeliveryReportByOperationId(
                        _fixture.PostgresConnectionString,
                        operationId);
                    return deadReport is not null;
                },
                maxAttempts: 20,
                delayMs: 500);

            Assert.NotNull(deadReport);
            Assert.False(deadReport.Resolved);
            Assert.Equal(1, deadReport.AttemptCount);
            Assert.Equal("UNRECOGNIZED_SEND_RESULT", deadReport.Reason);
            Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, deadReport.Channel);
            Assert.True(deadReportFound, "Dead delivery report should be saved when SendResult is unrecognized");

            // Assert - ArgumentException fires before the service is reached; UpdateEmailSendStatus must never be called
            mockService.Verify(
                s => s.UpdateSendStatus(It.IsAny<Core.Models.Notification.EmailSendOperationResult>()),
                Times.Never);

            // Assert - Message is discarded, not moved to the dead letter queue
            var dlqEmpty = await ServiceBusTestUtils.WaitForDeadLetterEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(dlqEmpty, "Dead letter queue should be empty — unrecognized SendResult is saved to dead delivery reports, not DLQ");

            // Assert - Queue itself is empty (no retries; ArgumentException is not retried)
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty — unknown SendResult should not be retried");
        }
    }

    [Fact]
    public async Task EmailSendResult_WhenNotificationNotFound_RetriesAndSavesDeadDeliveryReport()
    {
        // Arrange - Capture logs to count handler attempts via NotificationNotFoundException
        var logCapture = new LogCapture(nameof(NotificationNotFoundException));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(logCapture))
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.EmailSendResultQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
            string queueName = factory.WolverineSettings!.EmailSendResultQueueName;
            string operationId = Guid.NewGuid().ToString();

            // Act - Send a command with a notificationId that doesn't exist in the database
            var command = new EmailSendResultCommand
            {
                NotificationId = Guid.NewGuid(),
                OperationId = operationId,
                SendResult = EmailNotificationResultType.Delivered.ToString()
            };
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Poll the dead delivery reports table until the report appears after retries exhaust
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetDeadDeliveryReportByOperationId(
                        _fixture.PostgresConnectionString,
                        operationId);
                    return deadReport is not null;
                },
                maxAttempts: 40,
                delayMs: 500);
            Assert.True(deadReportFound, "Dead delivery report should be saved after retries are exhausted");
            Assert.Equal("RETRY_THRESHOLD_EXCEEDED", deadReport!.Reason);
            Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, deadReport.Channel);
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
    public async Task EmailSendResult_WhenNotificationExpired_SavesDeadDeliveryReportWithoutRetry()
    {
        // Arrange - Capture logs to verify the handler only runs once (no retries for expired)
        var logCapture = new LogCapture(nameof(NotificationExpiredException));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(logCapture))
            .Initialize();

        await using (factory)
        {
            // Arrange - Create notification, set operationId via Succeeded status,
            // then expire it. Must set operationId before expiring — the SQL function
            // blocks updates on expired notifications.
            string operationId = Guid.NewGuid().ToString();
            string queueName = factory.WolverineSettings!.EmailSendResultQueueName;
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            await PostgreUtil.UpdateEmailSendStatus(factory, notification.Id, EmailNotificationResultType.Succeeded, operationId);

            // Now expire the notification by backdating its expiry time
            await PostgreUtil.RunSql(
                _fixture.PostgresConnectionString,
                "UPDATE notifications.emailnotifications SET expirytime = now() - interval '1 minute' WHERE alternateid = $1",
                new NpgsqlParameter { Value = notification.Id });

            // Act - Send send-result command for the expired notification
            var command = new EmailSendResultCommand
            {
                NotificationId = notification.Id,
                OperationId = operationId,
                SendResult = EmailNotificationResultType.Delivered.ToString()
            };
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Poll the dead delivery reports table until the report appears
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetDeadDeliveryReportByOperationId(
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
            Assert.True(queueEmpty, "Queue should be empty — expired notifications are not retried");

            // Assert - DLQ is empty
            var dlqEmpty = await ServiceBusTestUtils.WaitForDeadLetterEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(dlqEmpty, "Dead letter queue should be empty — NotificationExpiredException should not trigger DLQ");

            // Assert - Verify the handler encountered the exception exactly once (no retries)
            Console.WriteLine($"[Test] NotificationExpiredException logged {logCapture.Count} times");
            Assert.Equal(1, logCapture.Count);
        }
    }

    [Fact]
    public async Task EmailSendResult_WhenBothIdentifiersEmpty_SavesDeadDeliveryReportWithoutRetry()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailSendResultQueueName;

            // NotificationId = Guid.Empty and OperationId = null causes
            // InvalidNotificationIdentifierException in EmailNotificationRepository.UpdateSendStatus
            var command = new EmailSendResultCommand
            {
                NotificationId = Guid.Empty,
                OperationId = null,
                SendResult = EmailNotificationResultType.Delivered.ToString()
            };

            // Act
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Dead delivery report saved with the correct reason
            DeadDeliveryReportRow? deadReport = null;
            var deadReportFound = await WaitForUtils.WaitForAsync(
                async () =>
                {
                    deadReport = await PostgreUtil.GetLatestDeadDeliveryReportByReason(
                        _fixture.PostgresConnectionString,
                        "INVALID_NOTIFICATION_IDENTIFIER");
                    return deadReport is not null;
                },
                maxAttempts: 20,
                delayMs: 500);

            Assert.True(deadReportFound, "Dead delivery report should be saved when both identifiers are empty");
            Assert.Equal("INVALID_NOTIFICATION_IDENTIFIER", deadReport!.Reason);
            Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, deadReport.Channel);
            Assert.Equal(1, deadReport.AttemptCount);
            Assert.False(deadReport.Resolved);

            // Assert - Message discarded, not moved to DLQ
            var dlqEmpty = await ServiceBusTestUtils.WaitForDeadLetterEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(dlqEmpty, "Dead letter queue should be empty — InvalidNotificationIdentifierException should not trigger DLQ");

            // Assert - Queue is empty (no retries)
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty — invalid identifiers should not be retried");
        }
    }
}
