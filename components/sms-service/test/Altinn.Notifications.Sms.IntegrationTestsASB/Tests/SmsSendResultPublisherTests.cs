using System.Text.Json;

using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
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
/// Verifies that when the ASB publisher is enabled, calling <c>ISmsSendResultDispatcher.DispatchAsync</c>
/// sends a correctly shaped <see cref="SmsSendResultCommand"/> to the ASB queue.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SmsSendResultPublisherTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Happy path: the ASB publisher serializes a <see cref="SendOperationResult"/>
    /// into a flat <see cref="SmsSendResultCommand"/> and sends it to the configured queue.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenEnabled_SendsCommandToQueue()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange
            var dispatcher = factory.Host.Services.GetRequiredService<ISmsSendResultDispatcher>();
            var result = new SendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = Guid.NewGuid().ToString(),
                SendResult = SmsSendResult.Accepted
            };

            // Act
            await dispatcher.DispatchAsync(result);

            // Assert - Receive the message from the queue and verify its content
            string queueName = factory.WolverineSettings!.SmsSendResultQueueName;
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
    }

    /// <summary>
    /// End-to-end: <see cref="ISendingService"/> accepts the SMS via a mocked
    /// <see cref="ISmsClient"/>, and the resulting <see cref="SendOperationResult"/>
    /// is published by the ASB dispatcher as a flat command on the queue.
    /// </summary>
    [Fact]
    public async Task SendingService_WhenPublisherEnabled_PublishesSendResultToQueue()
    {
        var notificationId = Guid.NewGuid();
        var gatewayReference = Guid.NewGuid().ToString();

        var smsClientMock = new Mock<ISmsClient>();
        smsClientMock
            .Setup(c => c.SendAsync(It.IsAny<Core.Sending.Sms>()))
            .ReturnsAsync(gatewayReference);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => smsClientMock.Object)
            .Initialize();

        await using (factory)
        {
            // Arrange
            var sendingService = factory.Host.Services.GetRequiredService<ISendingService>();
            var sms = new Core.Sending.Sms(notificationId, "sender", "+4799999999", "Integration test SMS body");

            // Act
            await sendingService.SendAsync(sms);

            // Assert - Receive the message from the queue
            string queueName = factory.WolverineSettings!.SmsSendResultQueueName;
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
