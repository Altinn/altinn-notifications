using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for <see cref="IComposedEmailCommandPublisher"/> and its implementation.
/// Verifies that composed email notifications are correctly mapped to <see cref="SendComposedEmailCommand"/>
/// and delivered to the Azure Service Bus queue via Wolverine.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class ComposedEmailCommandPublisherTests(IntegrationTestApiAsbContainersFixture fixture)
{
    private const string _queueName = "altinn.notifications.composedemail.send";

    private static readonly TimeSpan _messageTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan _noMessageTimeout = TimeSpan.FromSeconds(5);

    private static readonly Uri _sasUrl = new("https://storage.example.com/container/file.pdf?sv=2021&sig=abc");

    [Fact]
    public async Task PublishAsync_MultipleCalls_EachDeliversIndependentCommandToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var firstEmail = CreateEmail(subject: "Plain Subject", body: "Plain Body", toAddress: "plain@altinnxyz.no");
        var secondEmail = CreateEmail(subject: "Html Subject", body: "<p>Html Body</p>", toAddress: "html@altinnxyz.no", contentType: EmailContentType.Html);

        // Act
        await publisher.PublishAsync(firstEmail, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(secondEmail, TestContext.Current.CancellationToken);

        // Assert
        var firstMessage = await ReceiveMessageAsync(_messageTimeout);
        var secondMessage = await ReceiveMessageAsync(_messageTimeout);

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);

        var firstCommand = ReadCommand(firstMessage);
        var secondCommand = ReadCommand(secondMessage);

        Assert.NotNull(firstCommand);
        Assert.NotNull(secondCommand);

        var receivedIds = new[] { firstCommand.NotificationId, secondCommand.NotificationId };
        Assert.Contains(firstEmail.NotificationId, receivedIds);
        Assert.Contains(secondEmail.NotificationId, receivedIds);
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
    public async Task PublishAsync_ValidComposedEmail_DeliversCommandWithAttachmentsMappedToQueue()
    {
        // Arrange
        var publisher = await CreatePublisherAsync();
        var attachment = new SasFileReference { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = _sasUrl };
        var email = CreateEmail(attachments: [attachment]);

        // Act
        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        // Assert
        var message = await ReceiveMessageAsync(_messageTimeout);
        Assert.NotNull(message);

        var command = ReadCommand(message);

        Assert.NotNull(command);

        var firstAttachment = Assert.Single(command.Attachments);
        Assert.Equal("report.pdf", firstAttachment.Filename);
        Assert.Equal(_sasUrl.ToString(), firstAttachment.SasUrl);
        Assert.Equal("application/pdf", firstAttachment.MimeType);
    }

    [Fact]
    public async Task PublishAsync_ValidComposedEmail_DeliversCommandWithAllBaseFieldsMappedToQueue()
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

        Assert.Empty(command.Attachments);
        Assert.Equal("Hello", command.Subject);
        Assert.Equal("<p>World</p>", command.Body);
        Assert.Equal("sender@altinnxyz.no", command.FromAddress);
        Assert.Equal("recipient@altinnxyz.no", command.ToAddress);
        Assert.Equal(email.NotificationId, command.NotificationId);
        Assert.Equal(EmailContentType.Html.ToString(), command.ContentType);
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

    private async Task<IComposedEmailCommandPublisher> CreatePublisherAsync()
    {
        fixture.ResetInstalledMocks();
        await fixture.DrainQueueAsync(_queueName);

        return fixture.WebHost.Host.Services.GetRequiredService<IComposedEmailCommandPublisher>();
    }

    private Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan timeout)
    {
        return ServiceBusTestUtils.WaitForMessageAsync(fixture.ServiceBusConnectionString, _queueName, timeout);
    }

    private static SendComposedEmailCommand? ReadCommand(ServiceBusReceivedMessage message)
    {
        return JsonSerializer.Deserialize<SendComposedEmailCommand>(message.Body.ToString());
    }

    private static ComposedEmail CreateEmail(
        string subject = "Subject",
        string body = "Body",
        string toAddress = "recipient@altinnxyz.no",
        EmailContentType contentType = EmailContentType.Plain,
        List<SasFileReference>? attachments = null)
    {
        return new ComposedEmail(Guid.NewGuid(), subject, body, "sender@altinnxyz.no", toAddress, contentType, attachments ?? []);
    }
}
