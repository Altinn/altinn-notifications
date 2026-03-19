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
    private const string EmailSendQueueName = "altinn.notifications.email.send";

    /// <summary>
    /// Verifies that publishing a valid email returns null (success indicator).
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidEmail_ReturnsNull()
    {
        var factory = CreateFactory();
        var email = new Email(Guid.NewGuid(), "Test Subject", "Test Body", "sender@example.com", "recipient@example.com", EmailContentType.Html);

        await using (factory)
        {
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
        var email = new Email(Guid.NewGuid(), "Test Subject", "Test Body", "sender@example.com", "recipient@example.com", EmailContentType.Plain);

        await using (factory)
        {
            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            await publisher.PublishAsync(email, CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                EmailSendQueueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(message);

            var command = JsonSerializer.Deserialize<SendEmailCommand>(message.Body.ToString());

            Assert.NotNull(command);
            Assert.Equal(EmailContentType.Plain.ToString(), command.ContentType);
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
        var email = new Email(Guid.NewGuid(), "Hello", "<p>World</p>", "sender@example.com", "recipient@example.com", EmailContentType.Html);

        await using (factory)
        {
            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(email, cts.Token));
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
        var idFirst = Guid.NewGuid();
        var idSecond = Guid.NewGuid();

        await using (factory)
        {
            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            await publisher.PublishAsync(
                new Email(idFirst, "First", "Body", "from@test.no", "to@test.no", EmailContentType.Html),
                CancellationToken.None);

            await publisher.PublishAsync(
                new Email(idSecond, "Second", "Body", "from@test.no", "to@test.no", EmailContentType.Html),
                CancellationToken.None);

            var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, EmailSendQueueName, TimeSpan.FromSeconds(10));

            var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString, EmailSendQueueName, TimeSpan.FromSeconds(10));

            Assert.NotNull(firstMessage);
            Assert.NotNull(secondMessage);

            var ids = new[]
            {
                JsonSerializer.Deserialize<SendEmailCommand>(firstMessage.Body.ToString())!.NotificationId,
                JsonSerializer.Deserialize<SendEmailCommand>(secondMessage.Body.ToString())!.NotificationId
            };

            Assert.Contains(idFirst, ids);
            Assert.Contains(idSecond, ids);
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
        var email = new Email(notificationId, "Hello", "<p>World</p>", "sender@example.com", "recipient@example.com", EmailContentType.Html);

        await using (factory)
        {
            var publisher = factory.Host.Services.GetRequiredService<IEmailCommandPublisher>();

            await publisher.PublishAsync(email, CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                EmailSendQueueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(message);

            var command = JsonSerializer.Deserialize<SendEmailCommand>(message.Body.ToString());

            Assert.NotNull(command);
            Assert.Equal(notificationId, command.NotificationId);
            Assert.Equal("Hello", command.Subject);
            Assert.Equal("<p>World</p>", command.Body);
            Assert.Equal("sender@example.com", command.FromAddress);
            Assert.Equal("recipient@example.com", command.ToAddress);
            Assert.Equal(EmailContentType.Html.ToString(), command.ContentType);
        }
    }

    private IntegrationTestWebApplicationFactory CreateFactory() =>
        new IntegrationTestWebApplicationFactory(_fixture).Initialize();
}
