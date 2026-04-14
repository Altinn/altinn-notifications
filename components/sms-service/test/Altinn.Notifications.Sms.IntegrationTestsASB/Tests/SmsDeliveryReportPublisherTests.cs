using System.Text.Json;

using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for the SMS delivery report ASB publisher.
/// Verifies that when the ASB publisher is enabled, calling <c>ISmsDeliveryReportPublisher.PublishAsync</c>
/// sends a correctly shaped <see cref="SmsDeliveryReportCommand"/> to the ASB queue.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SmsDeliveryReportPublisherTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Tests the happy path: the ASB publisher serializes a <see cref="SendOperationResult"/>
    /// into a flat <see cref="SmsDeliveryReportCommand"/> and sends it to the configured queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenEnabled_SendsCommandToQueue()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange
            var publisher = factory.Host.Services.GetRequiredService<ISmsDeliveryReportPublisher>();
            var result = new SendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                GatewayReference = Guid.NewGuid().ToString(),
                SendResult = SmsSendResult.Delivered
            };

            // Act
            await publisher.PublishAsync(result);

            // Assert - Receive the message from the queue and verify its content
            string queueName = factory.WolverineSettings!.SmsDeliveryReportQueueName;
            var received = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(received);

            var command = JsonSerializer.Deserialize<SmsDeliveryReportCommand>(received.Body.ToString());
            Assert.NotNull(command);
            Assert.Equal(result.NotificationId, command.NotificationId);
            Assert.Equal(result.GatewayReference, command.GatewayReference);
            Assert.Equal(result.SendResult.ToString(), command.SendResult);
        }
    }

    /// <summary>
    /// Tests that the StatusService integration works end-to-end:
    /// StatusService maps a DrMessage to SendOperationResult, which the publisher
    /// serializes into a flat command on the queue.
    /// </summary>
    [Fact]
    public async Task StatusService_WhenPublisherEnabled_PublishesDeliveryReportToQueue()
    {
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();

        await using (factory)
        {
            // Arrange
            var statusService = factory.Host.Services.GetRequiredService<IStatusService>();
            string gatewayReference = Guid.NewGuid().ToString();

            var drMessage = new LinkMobility.PSWin.Receiver.Model.DrMessage(
                gatewayReference,
                "12345678",
                LinkMobility.PSWin.Receiver.Model.DeliveryState.DELIVRD,
                DateTime.UtcNow.ToString("o"));

            // Act
            await statusService.UpdateStatusAsync(drMessage);

            // Assert - Receive the message from the queue
            string queueName = factory.WolverineSettings!.SmsDeliveryReportQueueName;
            var received = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                queueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(received);

            var command = JsonSerializer.Deserialize<SmsDeliveryReportCommand>(received.Body.ToString());
            Assert.NotNull(command);
            Assert.Equal(gatewayReference, command.GatewayReference);
            Assert.Equal("Delivered", command.SendResult);
        }
    }
}
