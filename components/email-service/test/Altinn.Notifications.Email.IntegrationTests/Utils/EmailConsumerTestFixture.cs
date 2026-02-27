using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Producers;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Email.IntegrationTests.Utils;

/// <summary>
/// Test fixture that provides properly configured components for email consumer integration tests.
/// </summary>
internal sealed class EmailConsumerTestFixture : IAsyncDisposable
{
    /// <summary>
    /// Gets the configured Kafka producer for sending test messages.
    /// </summary>
    public CommonProducer Producer { get; }

    /// <summary>
    /// Gets the configured email queue consumer for testing.
    /// </summary>
    public SendEmailQueueConsumer Consumer { get; }

    /// <summary>
    /// Gets the service provider containing all registered test dependencies.
    /// </summary>
    public ServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailConsumerTestFixture"/> class.
    /// </summary>
    public EmailConsumerTestFixture(CommonProducer producer, SendEmailQueueConsumer consumer, ServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        Consumer = consumer;
        Producer = producer;
    }

    /// <summary>
    /// Disposes the test fixture components in the correct order to avoid disposal-related exceptions.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        Consumer?.Dispose();

        // The ServiceProvider will dispose the Producer.
        await ServiceProvider.DisposeAsync();
    }
}
