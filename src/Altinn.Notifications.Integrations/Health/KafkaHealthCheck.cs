using Confluent.Kafka;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Integrations.Health;

/// <summary>
/// Health check service confirming Kafka connenctivity
/// </summary>
public class KafkaHealthCheck : IHealthCheck, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _messageKey = Guid.NewGuid().ToString();
    private readonly string _healthCheckTopic;
    private bool _partitionAssigned;

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

        _producer = new ProducerBuilder<string, string>(config).Build();
        _consumer.Subscribe(_healthCheckTopic);

        Console.WriteLine("// Kafka Health Check // constructor completed");
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("// Kafka Health Check // Checking health.. ");

            // Produce a test message to the health check topic
            var testMessage = new Message<string, string> { Key = _messageKey, Value = "test" };
            var deliveryResult = await _producer.ProduceAsync(_healthCheckTopic, testMessage, cancellationToken);

            if (deliveryResult == null || deliveryResult.Status != PersistenceStatus.Persisted)
            {
                return HealthCheckResult.Unhealthy("Unable to produce test message on Kafka health check topic.");
            }

            // Ensure consumer reads from the same partition that is being written to
            if (!_partitionAssigned)
            {
                var partitions = new List<TopicPartitionOffset>
                {
                    new TopicPartitionOffset(_healthCheckTopic, deliveryResult.Partition, Offset.Beginning)
                };

                _consumer.Assign(partitions);
                _partitionAssigned = true;
            }

            // Consume the test message from the health check topic
            var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));

            if (consumeResult == null || consumeResult.Message.Value != "test")
            {
                return HealthCheckResult.Unhealthy("Unable to consume test message from Kafka health check topic.");
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
        _consumer.Close();
        _consumer.Dispose();
        _producer.Dispose();
    }
}