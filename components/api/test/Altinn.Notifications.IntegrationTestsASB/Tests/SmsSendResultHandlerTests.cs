using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
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

[Collection(nameof(IntegrationTestContainersCollection))]
public class SmsSendResultHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    [Fact]
    public async Task SmsSendResult_WhenNotificationExists_UpdatesStatusToDelivered()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange - Create notification and set status to Accepted with a gatewayReference
            var (_, notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(factory);
            string gatewayReference = Guid.NewGuid().ToString();
            await PostgreUtil.UpdateSmsSendStatus(factory, notification.Id, SmsNotificationResultType.Accepted, gatewayReference);

            string queueName = factory.WolverineSettings!.SmsSendResultQueueName;

            var command = new SmsSendResultCommand
            {
                NotificationId = notification.Id,
                GatewayReference = gatewayReference,
                SendResult = SmsNotificationResultType.Delivered.ToString()
            };

            // Act - Send the command to the ASB queue (simulating the SMS service)
            await factory.SendToQueueAsync(queueName, command);

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
        }
    }

    [Fact]
    public async Task SmsSendResult_WhenDatabaseThrowsNpgsqlException_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange - Create mock service that simulates database errors
        int attemptCount = 0;
        var mockService = new Mock<ISmsNotificationService>();
        mockService
            .Setup(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
            .Callback<SmsSendOperationResult>(_ => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new NpgsqlException("Simulated database error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.SmsSendResultQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;

            string queueName = factory.WolverineSettings.SmsSendResultQueueName;

            var command = new SmsSendResultCommand
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = Guid.NewGuid().ToString(),
                SendResult = SmsNotificationResultType.Delivered.ToString()
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
    public async Task SmsSendResult_WhenSendResultIsUnknown_SavesDeadDeliveryReportAndDiscardsMessage()
    {
        // Arrange - mock service to confirm the handler short-circuits before UpdateSendStatus
        var mockService = new Mock<ISmsNotificationService>(MockBehavior.Strict);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            string gatewayReference = Guid.NewGuid().ToString();
            string queueName = factory.WolverineSettings!.SmsSendResultQueueName;

            var command = new SmsSendResultCommand
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = gatewayReference,
                SendResult = "UnknownResultValue_XYZ"
            };

            // Act - Send command with an unrecognized SendResult value
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Dead delivery report should be saved to the database
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

            Assert.NotNull(deadReport);
            Assert.False(deadReport.Resolved);
            Assert.Equal(1, deadReport.AttemptCount);
            Assert.Equal("UNRECOGNIZED_SEND_RESULT", deadReport.Reason);
            Assert.Equal(DeliveryReportChannel.LinkMobility, deadReport.Channel);
            Assert.True(deadReportFound, "Dead delivery report should be saved when SendResult is unrecognized");

            // Assert - UnrecognizedSendResultException fires before the service is reached; UpdateSendStatus must never be called
            mockService.Verify(
                s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()),
                Times.Never);

            // Assert - Message is discarded, not moved to the dead letter queue
            var dlqEmpty = await ServiceBusTestUtils.WaitForDeadLetterEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(dlqEmpty, "Dead letter queue should be empty — unrecognized SendResult is saved to dead delivery reports, not DLQ");

            // Assert - Queue itself is empty (no retries; UnrecognizedSendResultException is not retried)
            var queueEmpty = await ServiceBusTestUtils.WaitForEmptyAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(5));
            Assert.True(queueEmpty, "Queue should be empty — unknown SendResult should not be retried");
        }
    }
}
