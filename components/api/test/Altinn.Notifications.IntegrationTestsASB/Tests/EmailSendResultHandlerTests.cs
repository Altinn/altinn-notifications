using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.IntegrationTestsASB.Utils;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Moq;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for the email sending status Wolverine handler.
/// Sends <see cref="EmailSendResultCommand"/> messages to the ASB queue (simulating the email service
/// polling loop) and verifies end-to-end processing through the handler pipeline.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailSendResultHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the happy path: an <see cref="EmailSendResultCommand"/> with result "Delivered" is received
    /// for a notification that exists in the database. The handler should update the status to "Delivered".
    /// </summary>
    [Fact]
    public async Task EmailSendResult_WhenNotificationExists_UpdatesStatusToDelivered()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - Create notification and set status to Succeeded with an operationId
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(factory);
            string operationId = Guid.NewGuid().ToString();
            await PostgreUtil.UpdateSendStatus(factory, notification.Id, EmailNotificationResultType.Succeeded, operationId);

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

    /// <summary>
    /// Tests that when the database throws <see cref="NpgsqlException"/>, the handler retries
    /// according to the configured policy and eventually moves the message to the dead letter queue.
    /// </summary>
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
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(30));
            Assert.NotNull(deadLetterMessage);

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
            // ScheduleRetry(500ms, 500ms, 500ms, 500ms, 500ms) = 5 more retries with new locks
            // Total: 1 initial + 3 cooldown retries + 5 scheduled retries = 9 attempts
            Console.WriteLine($"[Test] Handler was called {attemptCount} times");
            Assert.Equal(9, attemptCount);
        }
    }

    /// <summary>
    /// Tests that when the <c>SendResult</c> value is not a recognized <see cref="EmailNotificationResultType"/>,
    /// the handler throws <see cref="ArgumentException"/> and the message is moved immediately to the dead letter
    /// queue without retrying (ArgumentException is not in the retry policy).
    /// </summary>
    [Fact]
    public async Task EmailSendResult_WhenSendResultIsUnknown_MovesToDeadLetterQueueWithoutRetry()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailSendResultQueueName;

            var command = new EmailSendResultCommand
            {
                NotificationId = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                SendResult = "UnknownResultValue_XYZ"
            };

            // Act - Send command with an unrecognized SendResult value
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Message should appear in DLQ quickly (no retries for ArgumentException)
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(10));
            Assert.NotNull(deadLetterMessage);

            // Assert - Queue itself should be empty (not retried)
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty — unknown SendResult should not be retried");
        }
    }
}
