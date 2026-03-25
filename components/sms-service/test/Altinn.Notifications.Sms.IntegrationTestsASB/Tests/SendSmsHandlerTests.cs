using System.Text.Json;

using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

using Azure.Messaging.ServiceBus;

using Moq;

using Xunit;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Tests;

/// <summary>
/// Contains integration tests for the SendSmsHandler, verifying end-to-end behavior of processing a SendSmsCommand,
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SendSmsHandlerTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;

    /// <summary>
    /// Verifies that when a SendSmsCommand is received from the queue, the ISendingService.SendAsync method is invoked
    /// with an SMS object whose fields are correctly mapped from the command.
    /// </summary>
    /// <remarks>This integration test ensures that the SMS sending workflow correctly processes a
    /// SendSmsCommand by mapping its properties to the corresponding SMS object and invoking the sending service. The
    /// test waits for the service to be called and asserts that the mapped fields match the original command
    /// values.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task HandleAsync_WhenSendSmsCommandReceivedFromQueue_InvokesSendingServiceWithMappedFields()
    {
        // Arrange
        var command = new SendSmsCommand
        {
            NotificationId = Guid.NewGuid(),
            MobileNumber = "+4799999999",
            Body = "Integration test SMS body",
            SenderNumber = "Altinn"
        };

        bool sendAsyncCalled = false;
        var mockService = new Mock<ISendingService>();
        mockService
            .Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()))
            .Callback(() => sendAsyncCalled = true)
            .Returns(Task.CompletedTask);

        var factory = new IntegrationTestWebApplicationFactory(_fixture)
            .ReplaceService(_ => mockService.Object)
            .Initialize();

        await using (factory)
        {
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
            var queueName = factory.WolverineSettings.SmsSendQueueName;
            await using var sender = client.CreateSender(queueName);

            string body = JsonSerializer.Serialize(command);
            var message = new ServiceBusMessage(body);

            // Act
            await sender.SendMessageAsync(message);

            // Assert
            bool handled = await WaitForUtils.WaitForAsync(
                () => Task.FromResult(sendAsyncCalled),
                maxAttempts: 20,
                delayMs: 500);

            Assert.True(handled, "ISendingService.SendAsync was not called within the expected time.");

            mockService.Verify(
                s => s.SendAsync(It.Is<Core.Sending.Sms>(sms =>
                    sms.NotificationId == command.NotificationId &&
                    sms.Recipient == command.MobileNumber &&
                    sms.Message == command.Body &&
                    sms.Sender == command.SenderNumber)),
                Times.Once);
        }
    }
}
