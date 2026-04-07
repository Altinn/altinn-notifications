using System.Text.Json;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for <see cref="ISendSmsPublisher"/> and its implementation.
/// Verifies that SMS notifications are correctly mapped to <see cref="SendSmsCommand"/>
/// and delivered to the Azure Service Bus queue via Wolverine.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SendSmsCommandPublisherTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private const string _smsSendQueueName = "altinn.notifications.sms.send";

    /// <summary>
    /// Verifies that publishing a valid SMS returns null (success indicator).
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidSms_ReturnsNull()
    {
        var factory = CreateFactory();
        var sms = new Sms(
            notificationId: Guid.NewGuid(),
            sender: "Altinn",
            recipient: "+4799999999",
            message: "Integration test SMS");

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            var result = await publisher.PublishAsync(sms, CancellationToken.None);

            Assert.Null(result);
        }
    }

    /// <summary>
    /// Verifies that all fields from <see cref="Sms"/> are correctly mapped to
    /// <see cref="SendSmsCommand"/> properties when the message is delivered to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidSms_DeliversCommandWithAllFieldsMappedToQueue()
    {
        var factory = CreateFactory();
        var notificationId = Guid.NewGuid();
        var sms = new Sms(
            notificationId: notificationId,
            sender: "Altinn",
            recipient: "+4799999999",
            message: "Test message body");

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            await publisher.PublishAsync(sms, CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                _smsSendQueueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(message);

            var command = JsonSerializer.Deserialize<SendSmsCommand>(message.Body.ToString(), _jsonSerializerOptions);

            Assert.NotNull(command);
            Assert.Equal(notificationId, command.NotificationId);
            Assert.Equal("+4799999999", command.MobileNumber);
            Assert.Equal("Test message body", command.Body);
            Assert.Equal("Altinn", command.SenderNumber);
        }
    }

    /// <summary>
    /// Verifies that a pre-cancelled token causes <see cref="OperationCanceledException"/>
    /// to be thrown before the message is sent to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var factory = CreateFactory();
        var sms = new Sms(
            notificationId: Guid.NewGuid(),
            sender: "Altinn",
            recipient: "+4799999999",
            message: "Test message");

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(sms, cancellationTokenSource.Token));
        }
    }

    /// <summary>
    /// Verifies that multiple sequential publishes each deliver their own independent
    /// <see cref="SendSmsCommand"/> to the queue, proving each call creates a fresh scope.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleCalls_EachDeliversIndependentCommandToQueue()
    {
        var factory = CreateFactory();
        var firstSms = new Sms(
            notificationId: Guid.NewGuid(),
            sender: "Altinn",
            recipient: "+4711111111",
            message: "First message");
        var secondSms = new Sms(
            notificationId: Guid.NewGuid(),
            sender: "Altinn",
            recipient: "+4722222222",
            message: "Second message");

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            await publisher.PublishAsync(firstSms, CancellationToken.None);
            await publisher.PublishAsync(secondSms, CancellationToken.None);

            var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, _smsSendQueueName, TimeSpan.FromSeconds(10));

            var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, _smsSendQueueName, TimeSpan.FromSeconds(10));

            Assert.NotNull(firstMessage);
            Assert.NotNull(secondMessage);

            var commands = new[]
            {
                JsonSerializer.Deserialize<SendSmsCommand>(firstMessage.Body.ToString(), _jsonSerializerOptions),
                JsonSerializer.Deserialize<SendSmsCommand>(secondMessage.Body.ToString(), _jsonSerializerOptions)
            };

            var firstCommand = commands.Single(c => c!.NotificationId == firstSms.NotificationId);
            var secondCommand = commands.Single(c => c!.NotificationId == secondSms.NotificationId);

            Assert.NotNull(firstCommand);
            Assert.NotNull(secondCommand);

            Assert.Equal(firstSms.Message, firstCommand!.Body);
            Assert.Equal(firstSms.Recipient, firstCommand.MobileNumber);
            Assert.Equal(firstSms.Sender, firstCommand.SenderNumber);
            Assert.Equal(firstSms.NotificationId, firstCommand.NotificationId);

            Assert.Equal(secondSms.Message, secondCommand!.Body);
            Assert.Equal(secondSms.Recipient, secondCommand.MobileNumber);
            Assert.Equal(secondSms.Sender, secondCommand.SenderNumber);
            Assert.Equal(secondSms.NotificationId, secondCommand.NotificationId);
        }
    }

    private IntegrationTestWebApplicationFactory CreateFactory()
    {
        return new IntegrationTestWebApplicationFactory(_fixture).Initialize();
    }
}
