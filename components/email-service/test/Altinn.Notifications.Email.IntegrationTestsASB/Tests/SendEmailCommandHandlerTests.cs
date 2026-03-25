using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

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
            .WithConfig("WolverineSettings:EmailSendQueueName", "altinn.notifications.email.send")
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            // Act
            await factory.SendToEndpointAsync("altinn.notifications.email.send", command);
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
            .WithConfig("WolverineSettings:EmailSendQueueName", "altinn.notifications.email.send")
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            // Act
            await factory.SendToEndpointAsync("altinn.notifications.email.send", command);
            var capturedEmail = await sendingService.WaitForEmailAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotNull(capturedEmail);
            Assert.Equal(EmailContentType.Plain, capturedEmail.ContentType);
        }
    }
}
