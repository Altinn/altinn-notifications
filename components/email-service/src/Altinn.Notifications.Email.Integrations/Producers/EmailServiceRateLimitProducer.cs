using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;

namespace Altinn.Notifications.Email.Integrations.Producers;

/// <summary>
/// Kafka-based implementation of <see cref="IEmailServiceRateLimitDispatcher"/> that publishes
/// rate-limit events to a configured Kafka topic.
/// This implementation is used when the Azure Service Bus transport is disabled.
/// </summary>
public class EmailServiceRateLimitProducer : IEmailServiceRateLimitDispatcher
{
    private readonly string _topicName;
    private readonly ICommonProducer _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailServiceRateLimitProducer"/> class.
    /// </summary>
    /// <param name="producer">The Kafka producer used to publish the service update.</param>
    /// <param name="topicName">The Kafka topic name to publish to.</param>
    public EmailServiceRateLimitProducer(ICommonProducer producer, string topicName)
    {
        _producer = producer;
        _topicName = topicName;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(GenericServiceUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        bool success = await _producer.ProduceAsync(_topicName, update.Serialize());
        if (!success)
        {
            throw new InvalidOperationException("Failed to publish email service rate limit update.");
        }
    }
}
