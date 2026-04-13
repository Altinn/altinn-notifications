using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for the <see cref="CheckEmailSendStatusCommand"/> Wolverine handler.
/// Boots the real host with Wolverine and ASB emulator, sends commands through the message bus,
/// and verifies the handler polls ACS correctly and applies the configured retry policy.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class CheckEmailSendStatusHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    private static CheckEmailSendStatusCommand ValidCommand() => new()
    {
        NotificationId = Guid.NewGuid(),
        SendOperationId = Guid.NewGuid().ToString(),
        LastCheckedAtUtc = DateTime.UtcNow
    };

    /// <summary>
    /// Verifies that a terminal ACS result causes the handler to publish to Kafka via <see cref="ICommonProducer"/>.
    /// </summary>
    [Theory]
    [InlineData(EmailSendResult.Delivered)]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    public async Task CheckEmailSendStatus_WhenTerminalResult_PublishesToKafka(EmailSendResult terminalResult)
    {
        // Arrange
        var command = ValidCommand();
        var producerCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, _) => producerCalled.TrySetResult())
            .ReturnsAsync(true);

        var emailClientMock = new Mock<IEmailServiceClient>();
        emailClientMock
            .Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(terminalResult);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableEmailStatusCheckListener", "true")
            .ReplaceService<ICommonProducer>(_ => producerMock.Object)
            .ReplaceService<IEmailServiceClient>(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailStatusCheckQueueName;

            // Act
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Producer is called once ACS returns a terminal result
            var completed = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(producerCalled.Task.IsCompleted),
                maxAttempts: 20,
                delayMs: 500);
            Assert.True(completed, "Handler should publish to Kafka when ACS returns a terminal result");
        }
    }

    /// <summary>
    /// Verifies that when ACS returns <see cref="EmailSendResult.Sending"/>, the handler reschedules
    /// the command and eventually publishes to Kafka once a terminal result is received.
    /// </summary>
    [Fact]
    public async Task CheckEmailSendStatus_WhenStillSending_ReschedulesAndEventuallyPublishes()
    {
        // Arrange
        var command = ValidCommand();
        var producerCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, _) => producerCalled.TrySetResult())
            .ReturnsAsync(true);

        var emailClientMock = new Mock<IEmailServiceClient>();
        emailClientMock
            .SetupSequence(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(EmailSendResult.Sending)
            .ReturnsAsync(EmailSendResult.Delivered);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableEmailStatusCheckListener", "true")
            .ReplaceService<ICommonProducer>(_ => producerMock.Object)
            .ReplaceService<IEmailServiceClient>(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailStatusCheckQueueName;

            // Act
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Handler reschedules on first call (Sending) and publishes on second (Delivered)
            // Higher timeout to account for the 8-second rescheduling delay in the handler
            var completed = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(producerCalled.Task.IsCompleted),
                maxAttempts: 40,
                delayMs: 500);
            Assert.True(completed, "Handler should reschedule and eventually publish once ACS returns a terminal result");
        }
    }

    /// <summary>
    /// Verifies that an <see cref="InvalidOperationException"/> from the ACS client triggers the retry
    /// policy — cooldown retries followed by scheduled retries — before the message is moved to the DLQ.
    /// </summary>
    [Fact]
    public async Task CheckEmailSendStatus_WhenAcsClientThrows_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange
        int attemptCount = 0;

        var emailClientMock = new Mock<IEmailServiceClient>();
        emailClientMock
            .Setup(c => c.GetOperationUpdate(It.IsAny<string>()))
            .Callback(() => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new InvalidOperationException("Simulated ACS error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableEmailStatusCheckListener", "true")
            .ReplaceService<IEmailServiceClient>(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailStatusCheckQueueName;

            // Act
            await factory.SendToQueueAsync(queueName, ValidCommand());

            // Assert - Message should land in DLQ after retries are exhausted
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(30));
            Assert.NotNull(deadLetterMessage);

            // Assert - Verify the handler was called the expected number of times
            // RetryWithCooldown(100ms, 100ms, 100ms) = 3 retries within same lock
            // ScheduleRetry(500ms, 500ms, 500ms) = 3 more retries with new locks
            // Total: 1 initial + 3 cooldown retries + 3 scheduled retries = 7 attempts
            Console.WriteLine($"[Test] Handler was called {attemptCount} times");
            Assert.Equal(7, attemptCount);
        }
    }

    /// <summary>
    /// Verifies that a command with an empty <see cref="CheckEmailSendStatusCommand.NotificationId"/>
    /// is moved to the DLQ immediately — <see cref="ArgumentException"/> is not in the retry policy.
    /// </summary>
    [Fact]
    public async Task CheckEmailSendStatus_WhenNotificationIdIsEmpty_GoesToDeadLetterQueueWithoutRetry()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableEmailStatusCheckListener", "true")
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailStatusCheckQueueName;

            // Act - NotificationId = Guid.Empty triggers ArgumentException in the handler guard clause
            await factory.SendToQueueAsync(queueName, new CheckEmailSendStatusCommand
            {
                NotificationId = Guid.Empty,
                SendOperationId = Guid.NewGuid().ToString(),
                LastCheckedAtUtc = DateTime.UtcNow
            });

            // Assert - Message should appear in DLQ quickly (ArgumentException is not retried)
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(10));
            Assert.NotNull(deadLetterMessage);
        }
    }
}
