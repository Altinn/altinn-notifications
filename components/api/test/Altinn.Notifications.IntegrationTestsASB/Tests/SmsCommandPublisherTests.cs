using System.Text.Json;

using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for <see cref="ISmsCommandPublisher"/> and its implementation.
/// Verifies that SMS notifications are correctly mapped to <see cref="SendSmsCommand"/>
/// and delivered to the Azure Service Bus queue via Wolverine.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class SmsCommandPublisherTests(IntegrationTestApiAsbContainersFixture fixture)
{
    private static readonly TimeSpan _messageTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan _noMessageTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private string SmsSendQueueName => fixture.WebHost.WolverineSettings!.SendSmsQueueName;

    [Fact]
    public async Task PublishAsync_ValidSms_DeliversCommandWithAllFieldsMappedToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var sms = CreateSms(message: "Test message body");

        // Act
        await publisher.PublishAsync(sms, TestContext.Current.CancellationToken);

        // Assert
        var message = await ReceiveMessageAsync(_messageTimeout);
        Assert.NotNull(message);

        var command = ReadCommand(message);

        Assert.NotNull(command);
        Assert.Equal("Altinn", command.SenderNumber);
        Assert.Equal("Test message body", command.Body);
        Assert.Equal("+4799999999", command.MobileNumber);
        Assert.Equal(sms.NotificationId, command.NotificationId);
    }

    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException_AndDoesNotEnqueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var sms = CreateSms();

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(sms, cancellationTokenSource.Token));

        var queuedMessage = await ReceiveMessageAsync(_noMessageTimeout);
        Assert.Null(queuedMessage);
    }

    [Fact]
    public async Task PublishAsync_MultipleCalls_EachDeliversIndependentCommandToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var firstSms = CreateSms(recipient: "+4711111111", message: "First message");
        var secondSms = CreateSms(recipient: "+4722222222", message: "Second message");

        // Act
        await publisher.PublishAsync(firstSms, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(secondSms, TestContext.Current.CancellationToken);

        // Assert
        var firstMessage = await ReceiveMessageAsync(_messageTimeout);
        var secondMessage = await ReceiveMessageAsync(_messageTimeout);

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);

        var receivedCommands = new[] { ReadCommand(firstMessage), ReadCommand(secondMessage) };
        Assert.All(receivedCommands, Assert.NotNull);

        var firstCommand = Assert.Single(receivedCommands, c => c!.NotificationId == firstSms.NotificationId);
        var secondCommand = Assert.Single(receivedCommands, c => c!.NotificationId == secondSms.NotificationId);

        Assert.Equal(firstSms.Message, firstCommand!.Body);
        Assert.Equal(firstSms.Recipient, firstCommand.MobileNumber);
        Assert.Equal(firstSms.Sender, firstCommand.SenderNumber);

        Assert.Equal(secondSms.Message, secondCommand!.Body);
        Assert.Equal(secondSms.Recipient, secondCommand.MobileNumber);
        Assert.Equal(secondSms.Sender, secondCommand.SenderNumber);
    }

    [Fact]
    public async Task PublishAsync_SmsExceedsQueueMaxMessageSize_ThrowsNotificationSizeLimitExceededException_AndDoesNotEnqueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var sms = CreateSms(message: new string('a', 2 * 1024 * 1024));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotificationSizeLimitExceededException>(
            () => publisher.PublishAsync(sms, TestContext.Current.CancellationToken));

        Assert.Contains(sms.NotificationId.ToString(), exception.Message);

        var contentTooLarge = Assert.IsType<ContentTooLargeException>(exception.InnerException);
        var serviceBusException = Assert.IsType<ServiceBusException>(contentTooLarge.InnerException);
        Assert.Equal(ServiceBusFailureReason.MessageSizeExceeded, serviceBusException.Reason);

        var queuedMessage = await ReceiveMessageAsync(_noMessageTimeout);
        Assert.Null(queuedMessage);
    }

    [Fact]
    public async Task PublishAsync_ValidSmsAfterOversizedSms_IsDeliveredToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var oversizedSms = CreateSms(message: new string('a', 2 * 1024 * 1024));
        var validSms = CreateSms();

        await Assert.ThrowsAsync<NotificationSizeLimitExceededException>(
            () => publisher.PublishAsync(oversizedSms, TestContext.Current.CancellationToken));

        // Act
        await publisher.PublishAsync(validSms, TestContext.Current.CancellationToken);

        // Assert
        var message = await ReceiveMessageAsync(_messageTimeout);
        Assert.NotNull(message);

        var command = ReadCommand(message);
        Assert.NotNull(command);
        Assert.Equal(validSms.NotificationId, command.NotificationId);

        var unexpectedMessage = await ReceiveMessageAsync(_noMessageTimeout);
        Assert.Null(unexpectedMessage);
    }

    private async Task<ISmsCommandPublisher> CreatePublisherAsync()
    {
        fixture.ResetInstalledMocks();
        await fixture.DrainQueueAsync(SmsSendQueueName);

        return fixture.WebHost.Host.Services.GetRequiredService<ISmsCommandPublisher>();
    }

    private Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan timeout)
    {
        return ServiceBusTestUtils.WaitForMessageAsync(fixture.ServiceBusConnectionString, SmsSendQueueName, timeout);
    }

    private static SendSmsCommand? ReadCommand(ServiceBusReceivedMessage message)
    {
        return JsonSerializer.Deserialize<SendSmsCommand>(message.Body.ToString(), _jsonSerializerOptions);
    }

    private static Sms CreateSms(string sender = "Altinn", string recipient = "+4799999999", string message = "Test message")
    {
        return new Sms(notificationId: Guid.NewGuid(), sender: sender, recipient: recipient, message: message);
    }
}
