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
public class EmailServiceRateLimitPublisherTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    private static SendEmailCommand ValidSendEmailCommand() => new()
    {
        NotificationId = Guid.NewGuid(),
        Body = "Test body",
        ContentType = "Plain",
        Subject = "Test subject",
        FromAddress = "sender@example.com",
        ToAddress = "recipient@example.com"
    };

    [Fact]
    public async Task EmailServiceRateLimit_WhenAcsReturnsRateLimit_PublishesCommandToQueue()
    {
        // Arrange
        const int intermittentErrorDelaySeconds = 300;

        var emailClientMock = new Mock<IEmailServiceClient>();
        emailClientMock
            .Setup(e => e.SendEmail(It.IsAny<Core.Sending.Email>()))
            .ReturnsAsync(new EmailClientErrorResponse
            {
                IntermittentErrorDelay = intermittentErrorDelaySeconds,
                SendResult = EmailSendResult.Failed_TransientError
            });

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            string emailSendQueueName = factory.WolverineSettings!.EmailSendQueueName;
            string emailServiceRateLimitQueueName = factory.WolverineSettings.EmailServiceRateLimitQueueName;

            // Act
            await factory.SendToEndpointAsync(emailSendQueueName, ValidSendEmailCommand());

            // Assert
            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                emailServiceRateLimitQueueName,
                TimeSpan.FromSeconds(15));

            Assert.NotNull(message);

            var command = message.Body.ToObjectFromJson<EmailServiceRateLimitCommand>();
            Assert.NotNull(command);
            Assert.Equal("platform-notifications-email", command.Source);

            var data = JsonDocument.Parse(command.Data);
            Assert.True(data.RootElement.TryGetProperty("resource", out var resource), "Expected 'resource' field in Data JSON.");
            Assert.Equal("azure-communication-services-email", resource.GetString());
            Assert.True(data.RootElement.TryGetProperty("resetTime", out var resetTimeElement), "Expected 'resetTime' field in Data JSON.");

            var resetTime = resetTimeElement.GetDateTime();
            Assert.InRange(
                resetTime,
                DateTime.UtcNow.AddSeconds(intermittentErrorDelaySeconds - 30),
                DateTime.UtcNow.AddSeconds(intermittentErrorDelaySeconds + 30));
        }
    }

    [Fact]
    public async Task EmailServiceRateLimit_WhenAcsSendSucceeds_DoesNotPublishToRateLimitQueue()
    {
        // Arrange
        var clientInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Result<string, EmailClientErrorResponse> successResult = "acs-operation-id-123";

        var emailClientMock = new Mock<IEmailServiceClient>();
        emailClientMock
            .Setup(e => e.SendEmail(It.IsAny<Core.Sending.Email>()))
            .Callback<Core.Sending.Email>(_ => clientInvoked.TrySetResult())
            .ReturnsAsync(successResult);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            string emailSendQueueName = factory.WolverineSettings!.EmailSendQueueName;
            string emailServiceRateLimitQueueName = factory.WolverineSettings.EmailServiceRateLimitQueueName;

            // Act
            await factory.SendToEndpointAsync(emailSendQueueName, ValidSendEmailCommand());

            await clientInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                emailServiceRateLimitQueueName,
                TimeSpan.FromSeconds(3));

            Assert.Null(message);
        }
    }

    [Theory]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_Quarantined)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    [InlineData(EmailSendResult.Failed_InvalidEmailFormat)]
    [InlineData(EmailSendResult.Failed_SupressedRecipient)]
    public async Task EmailServiceRateLimit_WhenAcsReturnsNonTransientError_DoesNotPublishToRateLimitQueue(EmailSendResult nonTransientResult)
    {
        // Arrange
        var clientInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var emailClientMock = new Mock<IEmailServiceClient>();
        emailClientMock
            .Setup(e => e.SendEmail(It.IsAny<Core.Sending.Email>()))
            .Callback<Core.Sending.Email>(_ => clientInvoked.TrySetResult())
            .ReturnsAsync(new EmailClientErrorResponse { SendResult = nonTransientResult });

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => emailClientMock.Object)
            .Initialize();

        await using (factory)
        {
            string emailSendQueueName = factory.WolverineSettings!.EmailSendQueueName;
            string emailServiceRateLimitQueueName = factory.WolverineSettings.EmailServiceRateLimitQueueName;

            // Act
            await factory.SendToEndpointAsync(emailSendQueueName, ValidSendEmailCommand());

            await clientInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                emailServiceRateLimitQueueName,
                TimeSpan.FromSeconds(3));

            Assert.Null(message);
        }
    }
}
