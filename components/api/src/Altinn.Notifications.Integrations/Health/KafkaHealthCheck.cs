using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka;

using Confluent.Kafka;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Integrations.Health;

/// <summary>
/// Health check service confirming Kafka connenctivity
/// </summary>
public class KafkaHealthCheck : SharedClientConfig, IHealthCheck, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _healthCheckTopic;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaHealthCheck"/> class.
    /// </summary>
    public KafkaHealthCheck(KafkaSettings settings)
        : base(settings)
    {
        _healthCheckTopic = settings.HealthCheckTopic;

        var config = new ProducerConfig(ProducerSettings)
        {
            Acks = Acks.Leader,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Produce a test message to the health check topic
            var testMessage = new Message<Null, string> { Value = "test" };
            var deliveryResult = await _producer.ProduceAsync(_healthCheckTopic, testMessage, cancellationToken);

            if (deliveryResult == null || deliveryResult.Status != PersistenceStatus.Persisted)
            {
                return HealthCheckResult.Unhealthy("Unable to produce test message on Kafka health check topic.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the kafka producer and consumer
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        _producer.Dispose();
    }
}
