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
public class CheckEmailSendStatusHandlerTests(IntegrationTestEmailContainersFixture fixture)
{
    private IntegrationTestEmailContainersFixture _fixture = fixture;

    private string EmailStatusCheckQueueName => _fixture.WebHost.WolverineSettings!.EmailStatusCheckQueueName;

    private string EmailSendResultQueueName => _fixture.WebHost.WolverineSettings!.EmailSendResultQueueName;

    private async Task<Mock<IEmailServiceClient>> UseEmailClientMockAsync()
    {
        _fixture.ResetInstalledMocks();
        var emailClientMock = new Mock<IEmailServiceClient>();
        _fixture.InstallEmailServiceClient(emailClientMock.Object);
        return emailClientMock;
    }

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
        var emailClientMock = await UseEmailClientMockAsync();

        var webHost = _fixture.WebHost;
        emailClientMock
            .Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(terminalResult);
        await _fixture.DrainQueue(EmailStatusCheckQueueName);
        await _fixture.DrainQueue(EmailSendResultQueueName);

        // Act
        await webHost.SendToQueueAsync(EmailStatusCheckQueueName, command);

        // Assert - message should arrive on the sending status queue
        var receivedMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            EmailSendResultQueueName,
            TimeSpan.FromSeconds(15));

        Assert.NotNull(receivedMessage);

        var statusCommand = JsonSerializer.Deserialize<EmailSendResultCommand>(receivedMessage.Body.ToString());
        Assert.NotNull(statusCommand);
        Assert.Equal(command.NotificationId, statusCommand.NotificationId);
        Assert.Equal(command.SendOperationId, statusCommand.OperationId);
        Assert.Equal(terminalResult.ToString(), statusCommand.SendResult);
    }

    [Fact]
    public async Task CheckEmailSendStatus_WhenAcsClientThrows_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange
        int attemptCount = 0;
        var emailClientMock = await UseEmailClientMockAsync();
        var command = ValidCommand();

        var webHost = _fixture.WebHost;
        emailClientMock
            .Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .Callback(() =>
            {
                Interlocked.Increment(ref attemptCount);
            })
            .ThrowsAsync(new InvalidOperationException("Simulated ACS error"));

        await _fixture.DrainQueue(EmailStatusCheckQueueName);
        await _fixture.DrainQueue(EmailSendResultQueueName);

        var policy = _fixture.WebHost.WolverineSettings!.EmailStatusCheckQueuePolicy;
        int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;

        // Act
        await _fixture.WebHost.SendToQueueAsync(EmailStatusCheckQueueName, command);

        // Assert - Wait for message to appear in dead letter queue after retries exhaust
        var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
            _fixture.ServiceBusConnectionString,
            EmailStatusCheckQueueName,
            TimeSpan.FromSeconds(30));
        Assert.NotNull(deadLetterMessage);

        // Assert - Verify the handler was called exactly as many times as the policy dictates
        Console.WriteLine($"[Test] Handler was called {attemptCount} times (expected {expectedAttempts})");
        Assert.Equal(expectedAttempts, attemptCount);
    }

    [Fact]
    public async Task CheckEmailSendStatus_WhenNotificationIdIsEmpty_GoesToDeadLetterQueueWithoutRetry()
    {
        var webHost = _fixture.WebHost;
        await _fixture.DrainQueue(EmailStatusCheckQueueName);

        // Act - NotificationId = Guid.Empty triggers ArgumentException in the handler guard clause
        await webHost.SendToQueueAsync(EmailStatusCheckQueueName, new CheckEmailSendStatusCommand
        {
            NotificationId = Guid.Empty,
            SendOperationId = Guid.NewGuid().ToString(),
            LastCheckedAtUtc = DateTime.UtcNow
        });

        // Assert - Message should appear in DLQ quickly (ArgumentException is not retried)
        var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
            _fixture.ServiceBusConnectionString,
            EmailStatusCheckQueueName,
            TimeSpan.FromSeconds(10));
        Assert.NotNull(deadLetterMessage);
    }

    [Fact]
    public async Task CheckEmailSendStatus_WhenSendOperationIdIsEmpty_GoesToDeadLetterQueueWithoutRetry()
    {
        var webHost = _fixture.WebHost;
        await _fixture.DrainQueue(EmailStatusCheckQueueName);

        // Act - SendOperationId = string.Empty triggers ArgumentException in the handler guard clause.
        // ArgumentException is not in the CheckEmailSendStatusHandlerPolicy chain → DLQ immediately.
        await webHost.SendToQueueAsync(EmailStatusCheckQueueName, new CheckEmailSendStatusCommand
        {
            NotificationId = Guid.NewGuid(),
            SendOperationId = string.Empty,
            LastCheckedAtUtc = DateTime.UtcNow
        });

        // Assert - Message should appear in DLQ quickly (ArgumentException is not retried)
        var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
            _fixture.ServiceBusConnectionString,
            EmailStatusCheckQueueName,
            TimeSpan.FromSeconds(10));
        Assert.NotNull(deadLetterMessage);
    }
}
