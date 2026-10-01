using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

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
    private readonly IntegrationTestApiAsbContainersFixture _fixture = fixture;

    private const string _composedEmailSendQueueName = "altinn.notifications.composedemail.send";

    private static readonly Uri _sasUrl = new("https://storage.example.com/container/file.pdf?sv=2021&sig=abc");

    /// <summary>
    /// Verifies that two sequential valid publishes both succeed.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Multiple_SequentialSends_Succeed()
    {
        var factory = CreateFactory();
        var firstEmail = new ComposedEmail(Guid.NewGuid(), "Plain Subject", "Plain Body", "sender@altinnxyz.no", "plain@altinnxyz.no", EmailContentType.Plain, []);
        var secondEmail = new ComposedEmail(Guid.NewGuid(), "Html Subject", "<p>Html Body</p>", "sender@altinnxyz.no", "html@altinnxyz.no", EmailContentType.Html, []);

        await _fixture.DrainQueueAsync(_composedEmailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IComposedEmailCommandPublisher>();

        await publisher.PublishAsync(firstEmail, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(secondEmail, TestContext.Current.CancellationToken);

        var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _composedEmailSendQueueName,
            TimeSpan.FromSeconds(10));

        var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _composedEmailSendQueueName,
            TimeSpan.FromSeconds(10));

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);
    }

    /// <summary>
    /// Verifies that a pre-cancelled token causes <see cref="OperationCanceledException"/>
    /// to be thrown before the message is sent to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var factory = CreateFactory();
        var email = new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Plain, []);

        await _fixture.DrainQueueAsync(_composedEmailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IComposedEmailCommandPublisher>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(email, cts.Token));
    }

    /// <summary>
    /// Verifies that if nothing is published, the queue remains empty.
    /// </summary>
    [Fact]
    public async Task PublishAsync_NoEmailPublished_QueueRemainsEmpty()
    {
        var factory = CreateFactory();
        await _fixture.DrainQueueAsync(_composedEmailSendQueueName);

        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _composedEmailSendQueueName,
            TimeSpan.FromSeconds(5));

        Assert.Null(message);
    }

    /// <summary>
    /// Verifies that all base fields from <see cref="ComposedEmail"/> are correctly mapped to
    /// <see cref="SendComposedEmailCommand"/> properties when the message is delivered to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidComposedEmail_DeliversCommandWithAllBaseFieldsMappedToQueue()
    {
        var factory = CreateFactory();
        var notificationId = Guid.NewGuid();
        var email = new ComposedEmail(notificationId, "Hello", "<p>World</p>", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Html, []);

        await _fixture.DrainQueueAsync(_composedEmailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IComposedEmailCommandPublisher>();

        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _composedEmailSendQueueName,
            TimeSpan.FromSeconds(10));

        Assert.NotNull(message);

        var command = JsonSerializer.Deserialize<SendComposedEmailCommand>(message.Body.ToString());

        Assert.NotNull(command);
        Assert.Equal("Hello", command.Subject);
        Assert.Equal("<p>World</p>", command.Body);
        Assert.Equal(notificationId, command.NotificationId);
        Assert.Equal("sender@altinnxyz.no", command.FromAddress);
        Assert.Equal("recipient@altinnxyz.no", command.ToAddress);
        Assert.Equal(EmailContentType.Html.ToString(), command.ContentType);
    }

    /// <summary>
    /// Verifies that <see cref="ComposedEmail"/> attachments are correctly serialized as
    /// <see cref="SasFileAttachment"/> instances inside <see cref="SendComposedEmailCommand"/>.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidComposedEmail_DeliversCommandWithAttachmentsMappedToQueue()
    {
        var factory = CreateFactory();
        var attachment = new SasFileReference { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = _sasUrl };
        var email = new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Plain, [attachment]);

        await _fixture.DrainQueueAsync(_composedEmailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IComposedEmailCommandPublisher>();

        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _composedEmailSendQueueName,
            TimeSpan.FromSeconds(10));

        Assert.NotNull(message);

        var command = JsonSerializer.Deserialize<SendComposedEmailCommand>(message.Body.ToString());

        Assert.NotNull(command);
        Assert.Single(command.Attachments);

        var dto = command.Attachments[0];
        Assert.Equal("report.pdf", dto.Filename);
        Assert.Equal("application/pdf", dto.MimeType);
        Assert.Equal(_sasUrl.ToString(), dto.SasUrl);
    }

    /// <summary>
    /// Verifies that publishing multiple emails delivers one <see cref="SendComposedEmailCommand"/> per email to the queue,
    /// with all fields correctly mapped for each.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleEmails_DeliversAllCommandsToQueue()
    {
        var factory = CreateFactory();
        var plainEmail = new ComposedEmail(Guid.NewGuid(), "Plain Subject", "Plain Body", "sender@altinnxyz.no", "plain@altinnxyz.no", EmailContentType.Plain, []);
        var htmlEmail = new ComposedEmail(Guid.NewGuid(), "Html Subject", "<p>Html Body</p>", "sender@altinnxyz.no", "html@altinnxyz.no", EmailContentType.Html, []);
        await _fixture.DrainQueueAsync(_composedEmailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IComposedEmailCommandPublisher>();

        await publisher.PublishAsync(plainEmail, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(htmlEmail, TestContext.Current.CancellationToken);

        var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _composedEmailSendQueueName, TimeSpan.FromSeconds(10));

        var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _composedEmailSendQueueName, TimeSpan.FromSeconds(10));

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);

        var commands = new[]
        {
            JsonSerializer.Deserialize<SendComposedEmailCommand>(firstMessage.Body.ToString()),
            JsonSerializer.Deserialize<SendComposedEmailCommand>(secondMessage.Body.ToString())
        };

        var plainCommand = commands.Single(c => c!.NotificationId == plainEmail.NotificationId);
        var htmlCommand = commands.Single(c => c!.NotificationId == htmlEmail.NotificationId);

        Assert.NotNull(plainCommand);
        Assert.NotNull(htmlCommand);

        Assert.Equal(plainEmail.Body, plainCommand!.Body);
        Assert.Equal(plainEmail.Subject, plainCommand.Subject);
        Assert.Equal(plainEmail.ToAddress, plainCommand.ToAddress);
        Assert.Equal(plainEmail.FromAddress, plainCommand.FromAddress);
        Assert.Equal(plainEmail.ContentType.ToString(), plainCommand.ContentType);

        Assert.Equal(htmlEmail.Body, htmlCommand!.Body);
        Assert.Equal(htmlEmail.Subject, htmlCommand.Subject);
        Assert.Equal(htmlEmail.ToAddress, htmlCommand.ToAddress);
        Assert.Equal(htmlEmail.FromAddress, htmlCommand.FromAddress);
        Assert.Equal(htmlEmail.ContentType.ToString(), htmlCommand.ContentType);
    }

    private IntegrationTestWebApplicationFactory CreateFactory()
    {
        _fixture.ResetInstalledMocks();
        return _fixture.WebHost;
    }
}
