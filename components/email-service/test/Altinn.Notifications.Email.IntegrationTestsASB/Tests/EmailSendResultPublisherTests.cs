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
public class EmailSendResultPublisherTests(IntegrationTestEmailContainersFixture fixture)
{
    private readonly IntegrationTestEmailContainersFixture _fixture = fixture;

    private async Task<Mock<IEmailServiceClient>> UseEmailClientMockAsync()
    {
        var webHost = _fixture.WebHost;
        string checkQueueName = webHost.WolverineSettings!.EmailStatusCheckQueueName;
        string resultQueueName = webHost.WolverineSettings!.EmailSendResultQueueName;

        await _fixture.DrainQueue(checkQueueName);
        await _fixture.DrainQueue(resultQueueName);

        _fixture.ResetInstalledMocks();
        var emailClientMock = new Mock<IEmailServiceClient>();
        _fixture.InstallEmailServiceClient(emailClientMock.Object);
        return emailClientMock;
    }

    private static CheckEmailSendStatusCommand ValidCheckCommand() => new()
    {
        NotificationId = Guid.NewGuid(),
        LastCheckedAtUtc = DateTime.UtcNow,
        SendOperationId = Guid.NewGuid().ToString()
    };

    [Theory]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Delivered)]
    [InlineData(EmailSendResult.Succeeded)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_Quarantined)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    [InlineData(EmailSendResult.Failed_TransientError)]
    [InlineData(EmailSendResult.Failed_InvalidEmailFormat)]
    [InlineData(EmailSendResult.Failed_SupressedRecipient)]
    public async Task DispatchAsync_WhenTerminalResult_PublishesCommandWithCorrectFields(EmailSendResult terminalResult)
    {
        // Arrange
        var command = ValidCheckCommand();
        var emailClientMock = await UseEmailClientMockAsync();

        var webHost = _fixture.WebHost;
        emailClientMock
            .Setup(e => e.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(terminalResult);

        string checkQueueName = webHost.WolverineSettings!.EmailStatusCheckQueueName;
        string resultQueueName = webHost.WolverineSettings!.EmailSendResultQueueName;

        // Act
        await webHost.SendToQueueAsync(checkQueueName, command);

        // Assert
        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            resultQueueName,
            TimeSpan.FromSeconds(15));

        Assert.NotNull(message);

        var resultCommand = JsonSerializer.Deserialize<EmailSendResultCommand>(message.Body.ToString());
            
        Assert.NotNull(resultCommand);

        Assert.Equal(command.SendOperationId, resultCommand.OperationId);
        Assert.Equal(terminalResult.ToString(), resultCommand.SendResult);
        Assert.Equal(command.NotificationId, resultCommand.NotificationId);
    }
}
