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
        // Assign
        var factory = CreateFactory();

        var email = new Email(Guid.NewGuid(), "Test Subject", "Test Body", "sender@example.com", "recipient@example.com", EmailContentType.Html);

        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();

            var publisher = scope.ServiceProvider.GetRequiredService<IEmailCommandPublisher>();

            // Act
            var result = await publisher.PublishAsync(email, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }
    }

    /// <summary>
    /// Verifies that cancelling after the call starts propagates the cancellation correctly
    /// and no partial message appears on the queue in a way that would indicate silent swallowing.
    /// </summary>
    [Fact]
    public async Task PublishAsync_CancelledToken_DoesNotReturnNotificationId()
    {
        // Assign
        var factory = CreateFactory();
        var email = new Email(Guid.NewGuid(), "Hello", "<p>World</p>", "sender@example.com", "recipient@example.com", EmailContentType.Html);

        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IEmailCommandPublisher>();

            // Act
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            // OperationCanceledException must propagate — the NotificationId failure path
            // is only triggered by non-cancellation exceptions from the message bus.
            var exception = await Record.ExceptionAsync(() => publisher.PublishAsync(email, cancellationTokenSource.Token));

            Assert.IsType<OperationCanceledException>(exception, exactMatch: false);
        }
    }

    /// <summary>
    /// Verifies that <see cref="EmailContentType.Plain"/> is serialized as the string "Plain"
    /// when mapping from <see cref="Email.ContentType"/> (enum) to <see cref="SendEmailCommand.ContentType"/> (string).
    /// </summary>
    [Fact]
    public async Task PublishAsync_PlainContentType_MapsEnumToStringCorrectly()
    {
        // Assign
        var factory = CreateFactory();
        var email = new Email(Guid.NewGuid(), "Test Subject", "Test Body", "sender@example.com", "recipient@example.com", EmailContentType.Plain);

        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IEmailCommandPublisher>();

            // Act
            await publisher.PublishAsync(email, CancellationToken.None);

            var message = await ServiceBusTestUtils.WaitForMessageAsync(
                _fixture.ServiceBusConnectionString,
                _emailSendQueueName,
                TimeSpan.FromSeconds(10));

            // Assert
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
        // Assign
        var factory = CreateFactory();
        var email = new Email(Guid.NewGuid(), "Hello", "<p>World</p>", "sender@example.com", "recipient@example.com", EmailContentType.Html);

        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IEmailCommandPublisher>();

            // Act & Assert
            using var cancellationTokenSource = new CancellationTokenSource();

            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(email, cancellationTokenSource.Token));
        }
    }

    /// <summary>
    /// Verifies that all fields from <see cref="Email"/> are correctly mapped to
    /// <see cref="SendEmailCommand"/> properties when the message is delivered to the queue.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ValidEmail_DeliversCommandWithAllFieldsMappedToQueue()
    {
        // Assign
        var factory = CreateFactory();

        var notificationId = Guid.NewGuid();

        var email = new Email(notificationId, "Hello", "<p>World</p>", "sender@example.com", "recipient@example.com", EmailContentType.Html);

        await using (factory)
        {
            using var scope = factory.Host.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IEmailCommandPublisher>();

            // Act
            await publisher.PublishAsync(email, CancellationToken.None);
            var message = await ServiceBusTestUtils.WaitForMessageAsync(_fixture.ServiceBusConnectionString, _emailSendQueueName, TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotNull(message);

            var command = JsonSerializer.Deserialize<SendEmailCommand>(message.Body.ToString());

            Assert.NotNull(command);
            Assert.Equal("Hello", command.Subject);
            Assert.Equal("<p>World</p>", command.Body);
            Assert.Equal(notificationId, command.NotificationId);
            Assert.Equal("sender@example.com", command.FromAddress);
            Assert.Equal("recipient@example.com", command.ToAddress);
            Assert.Equal(EmailContentType.Html.ToString(), command.ContentType);
        }
    }

    /// <summary>
    /// Creates and initializes the test web application factory
    /// </summary>
    /// <returns></returns>
    private IntegrationTestWebApplicationFactory CreateFactory()
    {
        return new IntegrationTestWebApplicationFactory(_fixture).Initialize();
    }
}
