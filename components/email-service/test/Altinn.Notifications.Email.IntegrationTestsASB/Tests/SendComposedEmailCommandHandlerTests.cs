using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

[Collection(nameof(IntegrationTestContainersCollection))]
public class SendComposedEmailCommandHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private static readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(30);

    private static SendComposedEmailCommand ValidCommand(string contentType = "Plain") => new()
    {
        NotificationId = Guid.NewGuid(),
        Subject = "Test subject",
        Body = "Test body",
        ContentType = contentType,
        FromAddress = "sender@example.com",
        ToAddress = "recipient@example.com",
        Attachments =
        [
            new SasFileAttachment { Filename = "doc.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/doc.pdf?sas=token" }
        ]
    };

    [Fact]
    public async Task HandleAsync_ValidHtmlCommand_SendingServiceReceivesMappedComposedEmail()
    {
        // Arrange
        var sendingService = new AlwaysSucceedSendingService();
        var command = ValidCommand(contentType: "Html");

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.ComposedEmailSendQueueName;

            // Act
            await factory.SendToEndpointAsync(queueName, command);
            var capturedEmail = await sendingService.WaitForComposedEmailAsync(_sendTimeout);

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
        var command = ValidCommand(contentType: "Plain");

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.ComposedEmailSendQueueName;

            // Act
            await factory.SendToEndpointAsync(queueName, command);
            var capturedEmail = await sendingService.WaitForComposedEmailAsync(_sendTimeout);

            // Assert
            Assert.NotNull(capturedEmail);
            Assert.Equal(EmailContentType.Plain, capturedEmail.ContentType);
        }
    }

    [Fact]
    public async Task HandleAsync_UnknownContentType_DefaultsToPlainAndSendsSuccessfully()
    {
        // Arrange
        var sendingService = new AlwaysSucceedSendingService();
        var command = ValidCommand(contentType: "UnknownContentTypeXYZ");

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.ComposedEmailSendQueueName;

            // Act
            await factory.SendToEndpointAsync(queueName, command);
            var capturedEmail = await sendingService.WaitForComposedEmailAsync(_sendTimeout);

            // Assert - email is sent successfully with Plain as the fallback content type
            Assert.NotNull(capturedEmail);
            Assert.Equal(EmailContentType.Plain, capturedEmail.ContentType);
            Assert.Equal(command.NotificationId, capturedEmail.NotificationId);
        }
    }

    [Fact]
    public async Task HandleAsync_WithAttachments_AttachmentsPassedToSendingService()
    {
        // Arrange
        var sendingService = new AlwaysSucceedSendingService();
        var attachment = new SasFileAttachment
        {
            Filename = "report.xlsx",
            MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SasUrl = "https://storage.example.com/report.xlsx?sas=token"
        };
        var command = ValidCommand() with { Attachments = [attachment] };

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService<ISendingService>(_ => sendingService)
            .Initialize();

        await using (factory)
        {
            string queueName = factory.WolverineSettings!.ComposedEmailSendQueueName;

            // Act
            await factory.SendToEndpointAsync(queueName, command);
            var capturedEmail = await sendingService.WaitForComposedEmailAsync(_sendTimeout);

            // Assert
            Assert.NotNull(capturedEmail);
            Assert.Single(capturedEmail.Attachments);
            Assert.Equal(attachment.SasUrl, capturedEmail.Attachments[0].SasUrl);
            Assert.Equal(attachment.Filename, capturedEmail.Attachments[0].Filename);
            Assert.Equal(attachment.MimeType, capturedEmail.Attachments[0].MimeType);
        }
    }
}
