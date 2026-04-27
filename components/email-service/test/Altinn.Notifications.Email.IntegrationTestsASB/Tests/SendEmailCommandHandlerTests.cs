using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

[Collection(nameof(IntegrationTestContainersCollection))]
public class SendEmailCommandHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

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
            .ReplaceService(_ => sendingServiceMock.Object)
            .Initialize();

        await using (factory)
        {
            var policy = factory.WolverineSettings!.EmailSendQueuePolicy;
            int expectedAttempts = 1 + policy.CooldownDelaysMs.Length + policy.ScheduleDelaysMs.Length;
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
}
