using System.Text.Json;

using Altinn.Notifications.Core.Enums;
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
/// Integration tests for <see cref="IEmailCommandPublisher"/> and its implementation.
/// Verifies that email notifications are correctly mapped to <see cref="SendEmailCommand"/>
/// and delivered to the Azure Service Bus queue via Wolverine.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailCommandPublisherTests(IntegrationTestApiAsbContainersFixture fixture)
{
    private const string _queueName = "altinn.notifications.email.send";

    private static readonly TimeSpan _messageTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan _noMessageTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task PublishAsync_MultipleCalls_EachDeliversIndependentCommandToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var firstEmail = CreateEmail(subject: "First", body: "<p>message</p>", contentType: EmailContentType.Html);
        var secondEmail = CreateEmail(subject: "Second", body: "<p>message</p>");

        // Act
        await publisher.PublishAsync(firstEmail, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(secondEmail, TestContext.Current.CancellationToken);

        // Assert
        var firstMessage = await ReceiveMessageAsync(_messageTimeout);
        var secondMessage = await ReceiveMessageAsync(_messageTimeout);

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);

        var receivedCommands = new[] { ReadCommand(firstMessage), ReadCommand(secondMessage) };
        Assert.All(receivedCommands, Assert.NotNull);

        var firstCommand = Assert.Single(receivedCommands, c => c!.NotificationId == firstEmail.NotificationId);
        var secondCommand = Assert.Single(receivedCommands, c => c!.NotificationId == secondEmail.NotificationId);

        Assert.Equal(firstEmail.Body, firstCommand!.Body);
        Assert.Equal(firstEmail.Subject, firstCommand.Subject);
        Assert.Equal(firstEmail.ToAddress, firstCommand.ToAddress);
        Assert.Equal(firstEmail.FromAddress, firstCommand.FromAddress);
        Assert.Equal(firstEmail.ContentType.ToString(), firstCommand.ContentType);

        Assert.Equal(secondEmail.Body, secondCommand!.Body);
        Assert.Equal(secondEmail.Subject, secondCommand.Subject);
        Assert.Equal(secondEmail.ToAddress, secondCommand.ToAddress);
        Assert.Equal(secondEmail.FromAddress, secondCommand.FromAddress);
        Assert.Equal(secondEmail.ContentType.ToString(), secondCommand.ContentType);
    }

    [Fact]
    public async Task PublishAsync_ValidEmail_DeliversCommandWithAllFieldsMappedToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var email = CreateEmail(subject: "Hello", body: "<p>World</p>", contentType: EmailContentType.Html);

        // Act
        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        // Assert
        var message = await ReceiveMessageAsync(_messageTimeout);
        Assert.NotNull(message);

        var command = ReadCommand(message);

        Assert.NotNull(command);
        Assert.Equal("Hello", command.Subject);
        Assert.Equal("<p>World</p>", command.Body);
        Assert.Equal("sender@altinnxyz.no", command.FromAddress);
        Assert.Equal("recipient@altinnxyz.no", command.ToAddress);
        Assert.Equal(email.NotificationId, command.NotificationId);
        Assert.Equal(EmailContentType.Html.ToString(), command.ContentType);
    }

    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException_AndDoesNotEnqueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var email = CreateEmail();

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(email, cancellationTokenSource.Token));

        var queuedMessage = await ReceiveMessageAsync(_noMessageTimeout);
        Assert.Null(queuedMessage);
    }

    [Fact]
    public async Task PublishAsync_EmailExceedsQueueMaxMessageSize_ThrowsNotificationSizeLimitExceededException_AndDoesNotEnqueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var email = CreateEmail(body: new string('a', 2 * 1024 * 1024));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotificationSizeLimitExceededException>(
            () => publisher.PublishAsync(email, TestContext.Current.CancellationToken));

        Assert.Contains(email.NotificationId.ToString(), exception.Message);

        var contentTooLarge = Assert.IsType<ContentTooLargeException>(exception.InnerException);
        var serviceBusException = Assert.IsType<ServiceBusException>(contentTooLarge.InnerException);
        Assert.Equal(ServiceBusFailureReason.MessageSizeExceeded, serviceBusException.Reason);

        var queuedMessage = await ReceiveMessageAsync(_noMessageTimeout);
        Assert.Null(queuedMessage);
    }

    [Fact]
    public async Task PublishAsync_ValidEmailAfterOversizedEmail_IsDeliveredToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var oversizedEmail = CreateEmail(body: new string('a', 2 * 1024 * 1024));
        var validEmail = CreateEmail();

        await Assert.ThrowsAsync<NotificationSizeLimitExceededException>(
            () => publisher.PublishAsync(oversizedEmail, TestContext.Current.CancellationToken));

        // Act
        await publisher.PublishAsync(validEmail, TestContext.Current.CancellationToken);

        // Assert
        var message = await ReceiveMessageAsync(_messageTimeout);
        Assert.NotNull(message);

        var command = ReadCommand(message);
        Assert.NotNull(command);
        Assert.Equal(validEmail.NotificationId, command.NotificationId);

        var unexpectedMessage = await ReceiveMessageAsync(_noMessageTimeout);
        Assert.Null(unexpectedMessage);
    }

    private async Task<IEmailCommandPublisher> CreatePublisherAsync()
    {
        fixture.ResetInstalledMocks();
        await fixture.DrainQueueAsync(_queueName);

        return fixture.WebHost.Host.Services.GetRequiredService<IEmailCommandPublisher>();
    }

    private Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan timeout)
    {
        return ServiceBusTestUtils.WaitForMessageAsync(fixture.ServiceBusConnectionString, _queueName, timeout);
    }

    private static SendEmailCommand? ReadCommand(ServiceBusReceivedMessage message)
    {
        return JsonSerializer.Deserialize<SendEmailCommand>(message.Body.ToString());
    }

    private static Email CreateEmail(
        string subject = "Subject",
        string body = "Body",
        string toAddress = "recipient@altinnxyz.no",
        EmailContentType contentType = EmailContentType.Plain)
    {
        return new Email(Guid.NewGuid(), subject, body, "sender@altinnxyz.no", toAddress, contentType);
    }
}
