using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Integrations.LinkMobility;
using Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

using Moq;

using Xunit;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Tests;

/// <summary>
/// Contains integration tests for the SendSmsCommandHandler, verifying end-to-end behavior of processing a SendSmsCommand,
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SendSmsCommandHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Verifies that when a SendSmsCommand is received from the queue, the ISendingService.SendAsync method is invoked
    /// with an SMS object whose fields are correctly mapped from the command.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenSendSmsCommandReceivedFromQueue_InvokesSendingServiceWithMappedFields()
    {
        // Arrange
        var command = new SendSmsCommand
        {
            NotificationId = Guid.NewGuid(),
            MobileNumber = "+4799999999",
            Body = "Integration test SMS body",
            SenderNumber = "Altinn"
        };

        int sendAsyncCallCount = 0;
        var mockService = new Mock<ISendingService>();
        mockService
            .Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()))
            .Callback(() => Interlocked.Increment(ref sendAsyncCallCount))
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            var queueName = GetQueueName(factory);

            // Act
            await factory.SendToQueueAsync(queueName, command);

            // Assert
            bool handled = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(Volatile.Read(ref sendAsyncCallCount) > 0),
                maxAttempts: 20,
                delayMs: 500,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(handled, "ISendingService.SendAsync was not called within the expected time.");

            mockService.Verify(
                s => s.SendAsync(It.Is<Core.Sending.Sms>(sms =>
                    sms.NotificationId == command.NotificationId &&
                    sms.Recipient == command.MobileNumber &&
                    sms.Message == command.Body &&
                    sms.Sender == command.SenderNumber)),
                Times.Once);
        }
    }

    /// <summary>
    /// Verifies that when the ISendingService.SendAsync method throws a transient exception (e.g., TaskCanceledException),
    /// the SendSmsCommandHandler retries the operation according to the configured retry policy.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task HandleAsync_WhenSendingServiceThrowsATransientException_RetriesAccordingToPolicy()
    {
        // Arrange
        var command = new SendSmsCommand
        {
            NotificationId = Guid.NewGuid(),
            MobileNumber = "+4799999999",
            Body = "Integration test SMS body",
            SenderNumber = "Altinn"
        };

        int callCount = 0;
        var mockService = new Mock<ISendingService>();
        mockService
            .Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()))
            .Callback(() => Interlocked.Increment(ref callCount))
            .ThrowsAsync(new TaskCanceledException("Transient error"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.SendSmsQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
            var queueName = GetQueueName(factory);

            // Act
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Verify the handler was called exactly as many times as the policy dictates
            bool handled = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(Volatile.Read(ref callCount) >= expectedAttempts),
                maxAttempts: 40,
                delayMs: 500,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(handled, "ISendingService.SendAsync was not retried as expected.");
            mockService.Verify(
                s => s.SendAsync(It.Is<Core.Sending.Sms>(sms =>
                    sms.NotificationId == command.NotificationId &&
                    sms.Recipient == command.MobileNumber &&
                    sms.Message == command.Body &&
                    sms.Sender == command.SenderNumber)),
                Times.Exactly(expectedAttempts));
        }
    }

    /// <summary>
    /// Verifies that when the ISendingService.SendAsync method throws a gateway error (rethrown as SmsGatewayException),
    /// the SendSmsCommandHandler retries the operation according to the gateway error policy, which skips
    /// in-lock cooldown retries and goes directly to spread-out scheduled retries.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenSendingServiceThrowsAGatewayException_RetriesAccordingToGatewayErrorPolicy()
    {
        // Arrange
        var command = new SendSmsCommand
        {
            NotificationId = Guid.NewGuid(),
            MobileNumber = "+4799999999",
            Body = "Integration test SMS body",
            SenderNumber = "Altinn"
        };

        int callCount = 0;
        var mockService = new Mock<ISendingService>();
        mockService
            .Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()))
            .Callback(() => Interlocked.Increment(ref callCount))
            .ThrowsAsync(new SmsGatewayException("Gateway error", new HttpRequestException("Gateway timeout")));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.SendSmsQueueGatewayErrorPolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
            var queueName = GetQueueName(factory);

            // Act
            await factory.SendToQueueAsync(queueName, command);

            // Assert - Verify the handler was called exactly as many times as the gateway error policy dictates
            bool handled = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(Volatile.Read(ref callCount) >= expectedAttempts),
                maxAttempts: 40,
                delayMs: 500,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(handled, "ISendingService.SendAsync was not retried as expected for gateway errors.");
            mockService.Verify(
                s => s.SendAsync(It.Is<Core.Sending.Sms>(sms =>
                    sms.NotificationId == command.NotificationId &&
                    sms.Recipient == command.MobileNumber &&
                    sms.Message == command.Body &&
                    sms.Sender == command.SenderNumber)),
                Times.Exactly(expectedAttempts));
        }
    }

    /// <summary>
    /// Verifies that when a SendSmsCommand has an empty NotificationId, the handler discards
    /// the message silently without invoking the sending service or dead-lettering the message.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNotificationIdIsEmpty_DiscardsMessageWithoutCallingService()
    {
        // Arrange
        var mockService = new Mock<ISendingService>();

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = GetQueueName(factory);

            // Act - NotificationId = Guid.Empty triggers the guard clause which logs and returns early
            await factory.SendToQueueAsync(queueName, new SendSmsCommand
            {
                NotificationId = Guid.Empty,
                MobileNumber = "+4799999999",
                Body = "Test SMS body",
                SenderNumber = "Altinn"
            });

            // Allow time for the message to be processed
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Assert - Message should NOT appear in the DLQ as it is discarded, not failed
            var deadLetterMessage = await ServiceBusTestUtils.WaitForDeadLetterMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(2));

            Assert.Null(deadLetterMessage);

            // Assert - The sending service should never have been called
            mockService.Verify(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()), Times.Never);
        }
    }

    private static string GetQueueName(IntegrationTestWebApplicationFactory factory)
    {
        return factory.WolverineSettings!.SendSmsQueueName;
    }
}
