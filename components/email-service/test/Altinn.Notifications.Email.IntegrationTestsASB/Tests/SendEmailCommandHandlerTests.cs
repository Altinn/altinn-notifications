using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for the <see cref="SendEmailCommand"/> Wolverine handler.
/// Boots the real host with Wolverine and ASB emulator, publishes commands through the message bus,
/// and verifies the handler invokes <see cref="ISendingService"/> with the correct mapped email.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SendEmailCommandHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Verifies that a <see cref="SendEmailCommand"/> published via Wolverine is handled
    /// end-to-end and the sending service receives a correctly mapped email with Html content type.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ValidHtmlCommand_SendingServiceReceivesMappedEmail()
    {
        // Arrange
        var sendingService = new AlwaysSucceedSendingService();
        var command = new SendEmailCommand
        {
            Body = "Body",
            ContentType = "Html",
            Subject = "Retry test",
            NotificationId = Guid.NewGuid(),
            FromAddress = "sender@example.com",
            ToAddress = "recipient@example.com"
        };

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableSendEmailListener", "true")
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailSendQueueName;

            // Act
            await factory.SendToEndpointAsync(queueName, command);
            var capturedEmail = await sendingService.WaitForEmailAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotNull(capturedEmail);
            Assert.Equal(command.Body, capturedEmail.Body);
            Assert.Equal(command.Subject, capturedEmail.Subject);
            Assert.Equal(command.ToAddress, capturedEmail.ToAddress);
            Assert.Equal(command.FromAddress, capturedEmail.FromAddress);
            Assert.Equal(EmailContentType.Html, capturedEmail.ContentType);
            Assert.Equal(command.NotificationId, capturedEmail.NotificationId);
        }
    }

    /// <summary>
    /// Verifies that a <see cref="SendEmailCommand"/> with Plain content type
    /// is correctly mapped and forwarded to the sending service.
    /// </summary>
    [Fact]
    public async Task HandleAsync_PlainContentType_MapsCorrectly()
    {
        // Arrange
        var sendingService = new AlwaysSucceedSendingService();
        var command = new SendEmailCommand
        {
            Body = "Body",
            ContentType = "Plain",
            Subject = "Retry test",
            NotificationId = Guid.NewGuid(),
            FromAddress = "sender@example.com",
            ToAddress = "recipient@example.com"
        };

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableSendEmailListener", "true")
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailSendQueueName;

            // Act
            await factory.SendToEndpointAsync(queueName, command);
            var capturedEmail = await sendingService.WaitForEmailAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotNull(capturedEmail);
            Assert.Equal(EmailContentType.Plain, capturedEmail.ContentType);
        }
    }

    /// <summary>
    /// Verifies that an <see cref="InvalidOperationException"/> thrown by the sending service
    /// triggers the retry policy — cooldown retries followed by scheduled retries — before
    /// the message is moved to the dead letter queue.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenSendingServiceThrows_RetriesAndMovesToDeadLetterQueue()
    {
        // Arrange
        int attemptCount = 0;
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Callback(() => Interlocked.Increment(ref attemptCount))
            .ThrowsAsync(new InvalidOperationException("Simulated sending failure"));

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:EnableSendEmailListener", "true")
            .ReplaceService<ISendingService>(_ => sendingServiceMock.Object)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.EmailSendQueueName;

            // Act
            await factory.SendToEndpointAsync(queueName, new SendEmailCommand
            {
                Body = "Body",
                ContentType = "Plain",
                Subject = "Subject",
                NotificationId = Guid.NewGuid(),
                FromAddress = "sender@example.com",
                ToAddress = "recipient@example.com"
            });

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
}
