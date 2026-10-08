using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

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
    private readonly IntegrationTestApiAsbContainersFixture _fixture = fixture;
    private const string _emailSendQueueName = "altinn.notifications.email.send";

    /// <summary>
    /// Verifies that multiple sequential publishes each deliver their own independent
    /// <see cref="SendEmailCommand"/> to the queue, proving each call creates a fresh scope.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleCalls_EachDeliversIndependentCommandToQueue()
    {
        var factory = CreateFactory();
        var firstEmail = new Email(Guid.NewGuid(), "First", "<p>message</p>", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Html);
        var secondEmail = new Email(Guid.NewGuid(), "Second", "<p>message</p>", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Plain);
        await _fixture.DrainQueueAsync(_emailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

        await publisher.PublishAsync(firstEmail, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(secondEmail, TestContext.Current.CancellationToken);

        var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(10));

        var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(10));

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);

        var commands = new[]
        {
            JsonSerializer.Deserialize<SendEmailCommand>(firstMessage.Body.ToString()),
            JsonSerializer.Deserialize<SendEmailCommand>(secondMessage.Body.ToString())
        };

        var firstCommand = commands.Single(c => c!.NotificationId == firstEmail.NotificationId);
        var secondCommand = commands.Single(c => c!.NotificationId == secondEmail.NotificationId);

        Assert.NotNull(firstCommand);
        Assert.NotNull(secondCommand);

        Assert.Equal(firstEmail.Body, firstCommand!.Body);
        Assert.Equal(firstEmail.Subject, firstCommand.Subject);
        Assert.Equal(firstEmail.ToAddress, firstCommand.ToAddress);
        Assert.Equal(firstEmail.FromAddress, firstCommand.FromAddress);
        Assert.Equal(firstEmail.NotificationId, firstCommand.NotificationId);
        Assert.Equal(firstEmail.ContentType.ToString(), firstCommand.ContentType);

        Assert.Equal(secondEmail.Body, secondCommand!.Body);
        Assert.Equal(secondEmail.Subject, secondCommand.Subject);
        Assert.Equal(secondEmail.ToAddress, secondCommand.ToAddress);
        Assert.Equal(secondEmail.FromAddress, secondCommand.FromAddress);
        Assert.Equal(secondEmail.NotificationId, secondCommand.NotificationId);
        Assert.Equal(secondEmail.ContentType.ToString(), secondCommand.ContentType);
    }

    /// <summary>
    /// Verifies that all fields from <see cref="Email"/> are correctly mapped to
    /// <see cref="SendEmailCommand"/> properties when the message is delivered to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidEmail_DeliversCommandWithAllFieldsMappedToQueue()
    {
        var factory = CreateFactory();
        var notificationId = Guid.NewGuid();
        var email = new Email(notificationId, "Hello", "<p>World</p>", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Html);
        await _fixture.DrainQueueAsync(_emailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _emailSendQueueName,
            TimeSpan.FromSeconds(10));

        Assert.NotNull(message);

        var command = JsonSerializer.Deserialize<SendEmailCommand>(message.Body.ToString());

        Assert.NotNull(command);
        Assert.Equal("Hello", command.Subject);
        Assert.Equal("<p>World</p>", command.Body);
        Assert.Equal(notificationId, command.NotificationId);
        Assert.Equal("sender@altinnxyz.no", command.FromAddress);
        Assert.Equal("recipient@altinnxyz.no", command.ToAddress);
        Assert.Equal(EmailContentType.Html.ToString(), command.ContentType);
    }

    /// <summary>
    /// Verifies that two sequential valid publishes both succeed.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Multiple_SequentialSends_Succeed()
    {
        var factory = CreateFactory();
        var firstEmail = new Email(Guid.NewGuid(), "Subject 1", "Body 1", "sender@altinnxyz.no", "recipient1@altinnxyz.no", EmailContentType.Plain);
        var secondEmail = new Email(Guid.NewGuid(), "Subject 2", "Body 2", "sender@altinnxyz.no", "recipient2@altinnxyz.no", EmailContentType.Html);

        await _fixture.DrainQueueAsync(_emailSendQueueName);

        var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

        await publisher.PublishAsync(firstEmail, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(secondEmail, TestContext.Current.CancellationToken);

        var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _emailSendQueueName,
            TimeSpan.FromSeconds(10));

        var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString,
            _emailSendQueueName,
            TimeSpan.FromSeconds(10));

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);
    }

    /// <summary>
    /// Verifies that publishing multiple emails delivers one <see cref="SendEmailCommand"/> per email to the queue,
    /// with all fields correctly mapped for each.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleEmails_DeliversAllCommandsToQueue()
    {
        var factory = CreateFactory();
        var firstEmail = new Email(Guid.NewGuid(), "First Subject", "First Body", "sender@altinnxyz.no", "first@altinnxyz.no", EmailContentType.Plain);
        var secondEmail = new Email(Guid.NewGuid(), "Second Subject", "Second Body", "sender@altinnxyz.no", "second@altinnxyz.no", EmailContentType.Html);
        await _fixture.DrainQueueAsync(_emailSendQueueName);
        var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

        await publisher.PublishAsync(firstEmail, TestContext.Current.CancellationToken);
        await publisher.PublishAsync(secondEmail, TestContext.Current.CancellationToken);

        var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(10));

        var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(10));

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);

        var commands = new[]
        {
            JsonSerializer.Deserialize<SendEmailCommand>(firstMessage.Body.ToString()),
            JsonSerializer.Deserialize<SendEmailCommand>(secondMessage.Body.ToString())
        };

        var firstCommand = commands.Single(c => c!.NotificationId == firstEmail.NotificationId);
        var secondCommand = commands.Single(c => c!.NotificationId == secondEmail.NotificationId);

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

    /// <summary>
    /// Verifies that if nothing is published, the queue remains empty.
    /// </summary>
    [Fact]
    public async Task PublishAsync_NoEmailPublished_QueueRemainsEmpty()
    {
        var factory = CreateFactory();

        await _fixture.DrainQueueAsync(_emailSendQueueName);
        var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

        Assert.Null(message);
    }

    /// <summary>
    /// Verifies that a pre-cancelled token causes <see cref="OperationCanceledException"/> to be thrown
    /// before the message is sent to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException_AndDoesNotEnqueue()
    {
        var factory = CreateFactory();
        var email = new Email(Guid.NewGuid(), "Subject", "Body", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Plain);
        await _fixture.DrainQueueAsync(_emailSendQueueName);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(email, cancellationTokenSource.Token));

        var message = await ServiceBusTestUtils.WaitForMessageAsync(
            _fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

        Assert.Null(message);
    }

    private IntegrationTestWebApplicationFactory CreateFactory()
    {
        _fixture.ResetInstalledMocks();
        return _fixture.WebHost;
    }
}
