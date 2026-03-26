using System.Text.Json;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Contains integration tests for the SendSmsPublisher class, ensuring that SMS messages are published correctly to the
/// SMS send queue.
/// </summary>
/// <remarks>This test class is part of the integration testing suite and utilizes a test fixture to set up the
/// necessary environment for testing SMS publishing functionality. It verifies that the correct payload is sent to the
/// SMS send queue and checks the integrity of the message received.</remarks>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SendSmsPublisherTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Asynchronously verifies that publishing an SMS message results in the message being sent to the SMS send queue
    /// with the expected payload.
    /// </summary>
    /// <remarks>This integration test ensures that the SMS publishing functionality correctly enqueues a
    /// message with the appropriate content and routing. It also checks that the result of the publish operation is
    /// null on success.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PublishAsync_SendsMessageToSmsSendQueue_WithCorrectPayload()
    {
        // Arrange
        var factory = new IntegrationTestWebApplicationFactory(_fixture).Initialize();
        await using (factory)
        {
            var serviceProvider = factory.Host.Services;
            var publisher = serviceProvider.GetRequiredService<ISendSmsPublisher>();

            var sms = new Sms(
                notificationId: Guid.NewGuid(),
                sender: "Altinn",
                recipient: "+4799999999",
                message: "Integration test SMS");

            // Act
            var result = await publisher.PublishAsync(sms, default);

            // Assert: message should be sent to the correct queue
            var queueName = factory.WolverineSettings.SmsSendQueueName;
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
            var receiver = client.CreateReceiver(queueName);

            var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(received);

            // Optionally, verify the message body
            var command = JsonSerializer.Deserialize<SendSmsCommand>(received.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(command);
            Assert.Equal(sms.NotificationId, command.NotificationId);
            Assert.Equal(sms.Recipient, command.MobileNumber);
            Assert.Equal(sms.Message, command.Body);
            Assert.Equal(sms.Sender, command.SenderNumber);

            // The result should be null on success
            Assert.Null(result);
        }
    }
}
