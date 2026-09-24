using System.Text.Json;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Moq;
using Xunit;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailServiceRateLimitPublisherTests(IntegrationTestEmailContainersFixture fixture)
{
    private readonly IntegrationTestEmailContainersFixture _fixture = fixture;

    private Mock<IEmailServiceClient> UseEmailClientMock()
    {
        _fixture.ResetInstalledMocks();
        var emailClientMock = new Mock<IEmailServiceClient>();
        _fixture.InstallEmailServiceClient(emailClientMock.Object);
        return emailClientMock;
    }

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
        var emailClientMock = UseEmailClientMock();

        var webHost = _fixture.WebHost;
        emailClientMock
            .Setup(e => e.SendEmail(It.IsAny<Core.Sending.Email>()))
            .ReturnsAsync(new EmailClientErrorResponse
            {
                IntermittentErrorDelay = intermittentErrorDelaySeconds,
                SendResult = EmailSendResult.Failed_TransientError
            });

        string emailSendQueueName = webHost.WolverineSettings!.EmailSendQueueName;
        string emailServiceRateLimitQueueName = webHost.WolverineSettings!.EmailServiceRateLimitQueueName;

        await _fixture.DrainQueue(emailSendQueueName);
        await _fixture.DrainQueue(emailServiceRateLimitQueueName);

        // Act
        await webHost.SendToEndpointAsync(emailSendQueueName, ValidSendEmailCommand());

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

    [Fact]
    public async Task EmailServiceRateLimit_WhenAcsSendSucceeds_DoesNotPublishToRateLimitQueue()
    {
        // Arrange
        var clientInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var emailClientMock = UseEmailClientMock();

        Result<string, EmailClientErrorResponse> successResult = "acs-operation-id-123";

        var webHost = _fixture.WebHost;
        emailClientMock
            .Setup(e => e.SendEmail(It.IsAny<Core.Sending.Email>()))
            .Callback<Core.Sending.Email>(_ => clientInvoked.TrySetResult())
            .ReturnsAsync(successResult);

        string emailSendQueueName = webHost.WolverineSettings!.EmailSendQueueName;
        string emailServiceRateLimitQueueName = webHost.WolverineSettings!.EmailServiceRateLimitQueueName;

        await _fixture.DrainQueue(emailSendQueueName);
        await _fixture.DrainQueue(emailServiceRateLimitQueueName);

        // Act
        await webHost.SendToEndpointAsync(emailSendQueueName, ValidSendEmailCommand());

        await clientInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            emailServiceRateLimitQueueName,
            TimeSpan.FromSeconds(3));

        Assert.Null(message);
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
        var emailClientMock = UseEmailClientMock();

        var webHost = _fixture.WebHost;
        emailClientMock
            .Setup(e => e.SendEmail(It.IsAny<Core.Sending.Email>()))
            .Callback<Core.Sending.Email>(_ => clientInvoked.TrySetResult())
            .ReturnsAsync(new EmailClientErrorResponse { SendResult = nonTransientResult });

        string emailSendQueueName = webHost.WolverineSettings!.EmailSendQueueName;
        string emailServiceRateLimitQueueName = webHost.WolverineSettings!.EmailServiceRateLimitQueueName;

        // Act
        await webHost.SendToEndpointAsync(emailSendQueueName, ValidSendEmailCommand());

        await clientInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            emailServiceRateLimitQueueName,
            TimeSpan.FromSeconds(3));

        Assert.Null(message);
    }
}
