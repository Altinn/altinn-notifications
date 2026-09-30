using System.Text.Json;

using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for the SMS send-result ASB publisher.
/// Verifies that calling <c>ISmsSendResultDispatcher.DispatchAsync</c>
/// sends a correctly shaped <see cref="SmsSendResultCommand"/> to the ASB queue.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SmsSendResultPublisherTests(IntegrationTestSmsAsbContainersFixture fixture)
{
    private readonly IntegrationTestSmsAsbContainersFixture _fixture = fixture;

    [Fact]
    public async Task DispatchAsync_SendsCommandToQueue()
    {
        _fixture.ResetInstalledMocks();
        var factory = _fixture.WebHost;

        // Arrange
        var dispatcher = factory.Host.Services.GetRequiredService<ISmsSendResultDispatcher>();
        string queueName = factory.WolverineSettings!.SmsSendResultQueueName;
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = Guid.NewGuid().ToString(),
            SendResult = SmsSendResult.Accepted
        };

        // Act
        await _fixture.DrainQueue(queueName);
        await dispatcher.DispatchAsync(result);

        // Assert - Receive the message from the queue and verify its content
        var received = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            queueName,
            TimeSpan.FromSeconds(10));

        Assert.NotNull(received);

        var command = JsonSerializer.Deserialize<SmsSendResultCommand>(received.Body.ToString());
        Assert.NotNull(command);
        Assert.Equal(result.NotificationId, command.NotificationId);
        Assert.Equal(result.GatewayReference, command.GatewayReference);
        Assert.Equal(result.SendResult.ToString(), command.SendResult);
    }

    [Fact]
    public async Task SendingService_WhenPublisherEnabled_PublishesSendResultToQueue()
    {
        var notificationId = Guid.NewGuid();
        var gatewayReference = Guid.NewGuid().ToString();

        var smsClientMock = new Mock<ISmsClient>();
        smsClientMock
            .Setup(c => c.SendAsync(It.IsAny<Core.Sending.Sms>()))
            .ReturnsAsync(gatewayReference);

        _fixture.ResetInstalledMocks();
        _fixture.InstallService<ISmsClient>(smsClientMock.Object);
        var factory = _fixture.WebHost;
        {
            // Arrange
            var sendingService = factory.Host.Services.GetRequiredService<ISendingService>();
            var sms = new Core.Sending.Sms(notificationId, "sender", "+4799999999", "Integration test SMS body");
            string queueName = factory.WolverineSettings!.SmsSendResultQueueName;

            // Act
            await _fixture.DrainQueue(queueName);
            await sendingService.SendAsync(sms);

            // Assert - Receive the message from the queue
            var received = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(received);

            var command = JsonSerializer.Deserialize<SmsSendResultCommand>(received.Body.ToString());
            Assert.NotNull(command);
            Assert.Equal(notificationId, command.NotificationId);
            Assert.Equal(gatewayReference, command.GatewayReference);
            Assert.Equal("Accepted", command.SendResult);
        }
    }
}
