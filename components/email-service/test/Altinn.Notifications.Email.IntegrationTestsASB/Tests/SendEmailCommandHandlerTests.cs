using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Xunit;

using EmailMessage = Altinn.Notifications.Email.Core.Sending.Email;

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
        var spy = new SpySendingService();
        var command = new SendEmailCommand
        {
            NotificationId = Guid.NewGuid(),
            Subject = "Test Subject",
            Body = "<p>Hello</p>",
            FromAddress = "sender@example.com",
            ToAddress = "recipient@example.com",
            ContentType = "Html"
        };

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:AcceptEmailNotificationsViaWolverine", "true")
            .WithConfig("WolverineSettings:EmailSendQueueName", "altinn.notifications.email.send")
            .ReplaceService<ISendingService>(_ => spy)
            .Initialize();

        await using (factory)
        {
            // Act
            await factory.PublishMessageAsync(command);
            var capturedEmail = await spy.WaitForEmailAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotNull(capturedEmail);
            Assert.Equal(command.NotificationId, capturedEmail.NotificationId);
            Assert.Equal(command.Subject, capturedEmail.Subject);
            Assert.Equal(command.Body, capturedEmail.Body);
            Assert.Equal(command.FromAddress, capturedEmail.FromAddress);
            Assert.Equal(command.ToAddress, capturedEmail.ToAddress);
            Assert.Equal(EmailContentType.Html, capturedEmail.ContentType);
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
        var spy = new SpySendingService();
        var command = new SendEmailCommand
        {
            NotificationId = Guid.NewGuid(),
            Subject = "Plain email",
            Body = "Hello, World!",
            FromAddress = "sender@example.com",
            ToAddress = "recipient@example.com",
            ContentType = "Plain"
        };

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .WithConfig("WolverineSettings:AcceptEmailNotificationsViaWolverine", "true")
            .WithConfig("WolverineSettings:EmailSendQueueName", "altinn.notifications.email.send")
            .ReplaceService<ISendingService>(_ => spy)
            .Initialize();

        await using (factory)
        {
            // Act
            await factory.PublishMessageAsync(command);
            var capturedEmail = await spy.WaitForEmailAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotNull(capturedEmail);
            Assert.Equal(EmailContentType.Plain, capturedEmail.ContentType);
        }
    }

    /// <summary>
    /// Spy implementation of <see cref="ISendingService"/> that captures the email
    /// sent through the handler and signals completion via a <see cref="TaskCompletionSource"/>.
    /// </summary>
    private sealed class SpySendingService : ISendingService
    {
        private readonly TaskCompletionSource<EmailMessage> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EmailMessage? CapturedEmail { get; private set; }

        public Task SendAsync(EmailMessage email)
        {
            CapturedEmail = email;
            _tcs.TrySetResult(email);
            return Task.CompletedTask;
        }

        public async Task<EmailMessage?> WaitForEmailAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            cts.Token.Register(() => _tcs.TrySetCanceled());

            try
            {
                return await _tcs.Task;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }
    }
}
