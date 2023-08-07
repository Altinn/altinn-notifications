using Confluent.Kafka;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Integrations.Health;

/// <summary>
/// Health check service confirming Kafka connenctivity
/// </summary>
public class KafkaHealthCheck : IHealthCheck
{
    private readonly IProducer<Null, string> _producer;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _healthCheckTopic;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaHealthCheck"/> class.
    /// </summary>
    public KafkaHealthCheck(string brokerAddress, string healthCheckTopic, string consumerGroupId)
    {
        _healthCheckTopic = healthCheckTopic;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = brokerAddress,
            GroupId = consumerGroupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

        var config = new ProducerConfig
        {
            BootstrapServers = brokerAddress,
            Acks = Acks.Leader,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
        _consumer.Subscribe(_healthCheckTopic);
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Produce a test message to the health check topic
            var testMessage = new Message<Null, string> { Value = "test" };
            await _producer.ProduceAsync(_healthCheckTopic, testMessage, cancellationToken);

            // Consume the test message from the health check topic
            var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
            if (consumeResult == null || consumeResult.Message.Value != "test")
            {
                return HealthCheckResult.Unhealthy("Unable to consume test message from Kafka health check topic.");
            }

            _consumer.Commit(consumeResult);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}