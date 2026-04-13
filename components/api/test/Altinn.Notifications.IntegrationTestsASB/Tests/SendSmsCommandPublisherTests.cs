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
            string smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            var result = await publisher.PublishAsync(sms, CancellationToken.None);

            Assert.Null(result);

            // Drain the message from the queue so it doesn't pollute subsequent tests
            await ServiceBusTestUtils.WaitForMessageAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(10));
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
            var smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            await publisher.PublishAsync(sms, CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                smsSendQueueName,
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
            var smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(sms, cancellationTokenSource.Token));
            
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));
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
            var smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            await publisher.PublishAsync(firstSms, CancellationToken.None);
            await publisher.PublishAsync(secondSms, CancellationToken.None);

            var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(10));

            var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(10));

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

    /// <summary>
    /// Verifies that publishing a valid batch of SMS notifications returns an empty list (success indicator).
    /// </summary>
    [Fact]
    public async Task PublishAsync_Batch_AllSucceed_ReturnsEmptyList()
    {
        var factory = CreateFactory();
        var smsList = new List<Sms>
        {
            new(Guid.NewGuid(), "Altinn", "+4711111111", "First batch message"),
            new(Guid.NewGuid(), "Altinn", "+4722222222", "Second batch message")
        };

        await using (factory)
        {
            var smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            var result = await publisher.PublishAsync(smsList, CancellationToken.None);

            Assert.Empty(result);

            // Drain all published messages so the queue is clean for subsequent tests
            for (int i = 0; i < smsList.Count; i++)
            {
                await ServiceBusTestUtils.WaitForMessageAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(10));
            }
        }
    }

    /// <summary>
    /// Verifies that publishing a batch delivers one <see cref="SendSmsCommand"/> per SMS to the queue,
    /// with all fields correctly mapped for each.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Batch_ValidSmsList_DeliversAllCommandsToQueue()
    {
        var factory = CreateFactory();
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4711111111", "First batch message");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4722222222", "Second batch message");
        var smsList = new List<Sms> { firstSms, secondSms };

        await using (factory)
        {
            var smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            await publisher.PublishAsync(smsList, CancellationToken.None);

            var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(10));

            var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(10));

            Assert.NotNull(firstMessage);
            Assert.NotNull(secondMessage);

            var commands = new[]
            {
                JsonSerializer.Deserialize<SendSmsCommand>(firstMessage.Body.ToString(), _jsonSerializerOptions),
                JsonSerializer.Deserialize<SendSmsCommand>(secondMessage.Body.ToString(), _jsonSerializerOptions)
            };

            var firstCommand = commands.Single(c => c!.NotificationId == firstSms.NotificationId);
            var secondCommand = commands.Single(c => c!.NotificationId == secondSms.NotificationId);

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

    /// <summary>
    /// Verifies that publishing an empty batch returns an empty list without delivering any messages to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Batch_EmptyList_ReturnsEmptyListWithoutEnqueuingMessages()
    {
        var factory = CreateFactory();

        await using (factory)
        {
            var smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            var result = await publisher.PublishAsync([], CancellationToken.None);

            Assert.Empty(result);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            Assert.Null(message);
        }
    }

    /// <summary>
    /// Verifies that a pre-cancelled token causes <see cref="OperationCanceledException"/> to be thrown
    /// before any messages in the batch are sent to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Batch_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var factory = CreateFactory();
        var smsList = new List<Sms>
        {
            new(Guid.NewGuid(), "Altinn", "+4799999999", "Test message")
        };

        await using (factory)
        {
            var smsSendQueueName = GetQueueName(factory);
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<ISendSmsPublisher>();

            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(smsList, cancellationTokenSource.Token));

            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, smsSendQueueName, TimeSpan.FromSeconds(5));
        }
    }

    private IntegrationTestWebApplicationFactory CreateFactory()
    {
        return new IntegrationTestWebApplicationFactory(_fixture).Initialize();
    }

    private static string GetQueueName(IntegrationTestWebApplicationFactory factory)
    {
        return factory.WolverineSettings!.SendSmsQueueName;
    }
}
