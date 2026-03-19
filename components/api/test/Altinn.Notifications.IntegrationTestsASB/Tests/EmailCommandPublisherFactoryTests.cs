using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.IntegrationTestsASB.Infrastructure;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Tests;

/// <summary>
/// Integration tests for <see cref="IEmailCommandPublisherFactory"/> and its implementation.
/// Verifies that the factory correctly resolves publishers within scopes and delegates
/// publish operations to the underlying <see cref="IEmailCommandPublisher"/>.
/// </summary>
[Collection(nameof(IntegrationTestContainersCollection))]
public class EmailCommandPublisherFactoryTests(IntegrationTestContainersFixture fixture)
{
    private const string EmailSendQueueName = "altinn.notifications.email.send";

    private readonly IntegrationTestContainersFixture _fixture = fixture;

    private IntegrationTestWebApplicationFactory CreateFactory() =>
        new IntegrationTestWebApplicationFactory(_fixture).Initialize();

    private static Email CreateEmail(
        Guid? notificationId = null,
        string subject = "Test Subject",
        string body = "Test Body",
        string fromAddress = "sender@example.com",
        string toAddress = "recipient@example.com",
        EmailContentType contentType = EmailContentType.Html) =>
        new(
            notificationId ?? Guid.NewGuid(),
            subject,
            body,
            fromAddress,
            toAddress,
            contentType);

    /// <summary>
    /// Verifies that <see cref="IEmailCommandPublisherFactory"/> is registered as
    /// <see cref="EmailCommandPublisherFactory"/> in the DI container.
    /// </summary>
    [Fact]
    public async Task Factory_IsRegistered_AsEmailCommandPublisherFactory()
    {
        var factory = CreateFactory();
        await using (factory)
        {
            var publisherFactory = factory.Host.Services.GetRequiredService<IEmailCommandPublisherFactory>();

            Assert.IsType<EmailCommandPublisherFactory>(publisherFactory);
        }
    }

    /// <summary>
    /// Verifies that <see cref="IEmailCommandPublisherFactory.CreatePublisher"/> returns a
    /// non-null <see cref="IEmailCommandPublisher"/> instance.
    /// </summary>
    [Fact]
    public async Task CreatePublisher_ReturnsNonNullPublisherInstance()
    {
        var factory = CreateFactory();
        await using (factory)
        {
            var publisherFactory = factory.Host.Services.GetRequiredService<IEmailCommandPublisherFactory>();

            var publisher = publisherFactory.CreatePublisher();

            Assert.NotNull(publisher);
            Assert.IsAssignableFrom<IEmailCommandPublisher>(publisher);
        }
    }

    /// <summary>
    /// Verifies that <see cref="IEmailCommandPublisherFactory.PublishAsync"/> returns null on success,
    /// indicating the message was published without errors.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidEmail_ReturnsNull()
    {
        var factory = CreateFactory();
        await using (factory)
        {
            var publisherFactory = factory.Host.Services.GetRequiredService<IEmailCommandPublisherFactory>();
            var email = CreateEmail();

            var result = await publisherFactory.PublishAsync(email, CancellationToken.None);

            Assert.Null(result);
        }
    }

    /// <summary>
    /// Verifies end-to-end that the factory delegates publishing to the underlying publisher,
    /// and the <see cref="SendEmailCommand"/> arrives on the queue with correctly mapped fields.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ViaFactory_DeliversCommandWithAllFieldsMappedToQueue()
    {
        var factory = CreateFactory();
        await using (factory)
        {
            var notificationId = Guid.NewGuid();
            var email = CreateEmail(
                notificationId: notificationId,
                subject: "Factory Subject",
                body: "Factory Body",
                fromAddress: "factory-from@test.no",
                toAddress: "factory-to@test.no",
                contentType: EmailContentType.Html);

            var publisherFactory = factory.Host.Services.GetRequiredService<IEmailCommandPublisherFactory>();

            await publisherFactory.PublishAsync(email, CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                EmailSendQueueName,
                TimeSpan.FromSeconds(10));

            Assert.NotNull(message);

            var command = JsonSerializer.Deserialize<SendEmailCommand>(message.Body.ToString());

            Assert.NotNull(command);
            Assert.Equal(notificationId, command.NotificationId);
            Assert.Equal("Factory Subject", command.Subject);
            Assert.Equal("Factory Body", command.Body);
            Assert.Equal("factory-from@test.no", command.FromAddress);
            Assert.Equal("factory-to@test.no", command.ToAddress);
            Assert.Equal(EmailContentType.Html.ToString(), command.ContentType);
        }
    }

    /// <summary>
    /// Verifies that each call to <see cref="IEmailCommandPublisherFactory.PublishAsync"/>
    /// operates independently — multiple sequential publishes each deliver their own message to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleCalls_EachDeliversIndependentCommandToQueue()
    {
        var factory = CreateFactory();
        await using (factory)
        {
            var idFirst = Guid.NewGuid();
            var idSecond = Guid.NewGuid();
            var publisherFactory = factory.Host.Services.GetRequiredService<IEmailCommandPublisherFactory>();

            await publisherFactory.PublishAsync(
                CreateEmail(notificationId: idFirst, subject: "First"),
                CancellationToken.None);

            await publisherFactory.PublishAsync(
                CreateEmail(notificationId: idSecond, subject: "Second"),
                CancellationToken.None);

            var firstMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                EmailSendQueueName,
                TimeSpan.FromSeconds(10));

            var secondMessage = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                EmailSendQueueName,
                TimeSpan.FromSeconds(10));

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
    /// Verifies that a pre-cancelled token propagates <see cref="OperationCanceledException"/>
    /// through the factory without swallowing or converting it to a failure return value.
    /// </summary>
    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var factory = CreateFactory();
        await using (factory)
        {
            var publisherFactory = factory.Host.Services.GetRequiredService<IEmailCommandPublisherFactory>();
            var email = CreateEmail();

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => publisherFactory.PublishAsync(email, cts.Token));
        }
    }
}
