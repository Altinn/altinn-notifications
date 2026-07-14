using System.Text.Json;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

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

    [Theory]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Delivered)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    public async Task CheckEmailSendStatus_WhenTerminalResult_PublishesToAsbQueue(EmailSendResult terminalResult)
    {
        // Arrange
        var command = ValidCommand();

        var emailClientMock = new Mock<IEmailServiceClient>();
        emailClientMock
            .Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(terminalResult);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            string checkQueueName = factory.WolverineSettings!.EmailStatusCheckQueueName;
            string statusQueueName = factory.WolverineSettings!.EmailSendResultQueueName;

            // Act
            await factory.SendToQueueAsync(checkQueueName, command);

            // Assert - message should arrive on the sending status queue
            var receivedMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                statusQueueName,
                TimeSpan.FromSeconds(15));

            Assert.NotNull(receivedMessage);

            var statusCommand = JsonSerializer.Deserialize<EmailSendResultCommand>(receivedMessage.Body.ToString());
            Assert.NotNull(statusCommand);
            Assert.Equal(command.NotificationId, statusCommand.NotificationId);
            Assert.Equal(command.SendOperationId, statusCommand.OperationId);
            Assert.Equal(terminalResult.ToString(), statusCommand.SendResult);
        }
    }

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
            .ReplaceService(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.EmailStatusCheckQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
            string queueName = factory.WolverineSettings!.EmailStatusCheckQueueName;

            // Act
            await factory.SendToQueueAsync(queueName, ValidCommand());

            // Assert - Wait for message to appear in dead letter queue after retries exhaust
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(30));
            Assert.NotNull(deadLetterMessage);

            // Assert - Verify the handler was called exactly as many times as the policy dictates
            Console.WriteLine($"[Test] Handler was called {attemptCount} times (expected {expectedAttempts})");
            Assert.Equal(expectedAttempts, attemptCount);
        }
    }

    [Fact]
    public async Task CheckEmailSendStatus_WhenNotificationIdIsEmpty_GoesToDeadLetterQueueWithoutRetry()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture)
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

    [Fact]
    public async Task CheckEmailSendStatus_WhenSendOperationIdIsEmpty_GoesToDeadLetterQueueWithoutRetry()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailStatusCheckQueueName;

            // Act - SendOperationId = string.Empty triggers ArgumentException in the handler guard clause.
            // ArgumentException is not in the CheckEmailSendStatusHandlerPolicy chain → DLQ immediately.
            await factory.SendToQueueAsync(queueName, new CheckEmailSendStatusCommand
            {
                NotificationId = Guid.NewGuid(),
                SendOperationId = string.Empty,
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
