using System.Text.Json;

using Altinn.Notifications.Core.Enums;
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
/// Integration tests for <see cref="IEmailCommandPublisher"/> and its implementation.
/// Verifies that email notifications are correctly mapped to <see cref="SendEmailCommand"/>
/// and delivered to the Azure Service Bus queue via Wolverine.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailCommandPublisherTests(IntegrationTestContainersFixture fixture)
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private const string _emailSendQueueName = "altinn.notifications.email.send";

    /// <summary>
    /// Verifies that publishing a valid email returns null (success indicator).
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidEmail_ReturnsNull()
    {
        var factory = CreateFactory();
        var email = new Email(Guid.NewGuid(), "Test Subject", "Test Body", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Html);

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            var result = await publisher.PublishAsync(email, CancellationToken.None);

            Assert.Null(result);
        }
    }

    /// <summary>
    /// Verifies that <see cref="EmailContentType.Plain"/> is serialized as the string "Plain"
    /// when mapping from <see cref="Email.ContentType"/> (enum) to <see cref="SendEmailCommand.ContentType"/> (string).
    /// </summary>
    [Fact]
    public async Task PublishAsync_PlainContentType_MapsEnumToStringCorrectly()
    {
        var factory = CreateFactory();
        var email = new Email(Guid.NewGuid(), "Test Subject", "Test Body", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Plain);

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            await publisher.PublishAsync(email, CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                _emailSendQueueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(message);

            var sendEmailCommand = JsonSerializer.Deserialize<SendEmailCommand>(message.Body.ToString());

            Assert.NotNull(sendEmailCommand);
            Assert.Equal(EmailContentType.Plain.ToString(), sendEmailCommand.ContentType);
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
        var email = new Email(Guid.NewGuid(), "Hello", "<p>World</p>", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Html);

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(email, cancellationTokenSource.Token));
        }
    }

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

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            await publisher.PublishAsync(firstEmail, CancellationToken.None);
            await publisher.PublishAsync(secondEmail, CancellationToken.None);

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

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            await publisher.PublishAsync(email, CancellationToken.None);

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
    }

    /// <summary>
    /// Verifies that publishing a batch of valid emails returns an empty list (all succeeded).
    /// </summary>
    [Fact]
    public async Task PublishAsync_Batch_AllSucceed_ReturnsEmptyList()
    {
        var factory = CreateFactory();
        var emails = new List<Email>
        {
            new(Guid.NewGuid(), "Subject 1", "Body 1", "sender@altinnxyz.no", "recipient1@altinnxyz.no", EmailContentType.Plain),
            new(Guid.NewGuid(), "Subject 2", "Body 2", "sender@altinnxyz.no", "recipient2@altinnxyz.no", EmailContentType.Html)
        };

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            var result = await publisher.PublishAsync(emails, CancellationToken.None);

            Assert.Empty(result);
        }
    }

    /// <summary>
    /// Verifies that publishing a batch delivers one <see cref="SendEmailCommand"/> per email to the queue,
    /// with all fields correctly mapped for each.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Batch_ValidEmails_DeliversAllCommandsToQueue()
    {
        var factory = CreateFactory();
        var firstEmail = new Email(Guid.NewGuid(), "First Subject", "First Body", "sender@altinnxyz.no", "first@altinnxyz.no", EmailContentType.Plain);
        var secondEmail = new Email(Guid.NewGuid(), "Second Subject", "Second Body", "sender@altinnxyz.no", "second@altinnxyz.no", EmailContentType.Html);
        var emails = new List<Email> { firstEmail, secondEmail };

        await using (factory)
        {
            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            await publisher.PublishAsync(emails, CancellationToken.None);

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
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            var result = await publisher.PublishAsync([], CancellationToken.None);

            Assert.Empty(result);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

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
        var emails = new List<Email>
        {
            new(Guid.NewGuid(), "Subject", "Body", "sender@altinnxyz.no", "recipient@altinnxyz.no", EmailContentType.Plain)
        };

        await using (factory)
        {
            await ServiceBusTestUtils.WaitForEmptyAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(5));

            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(emails, cancellationTokenSource.Token));
        }
    }

    private IntegrationTestWebApplicationFactory CreateFactory()
    {
        return new IntegrationTestWebApplicationFactory(_fixture).Initialize();
    }
}
