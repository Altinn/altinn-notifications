using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Implementation of a Kafka producer
/// </summary>
public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<IKafkaProducer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaProducer"/> class.
    /// </summary>
    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<IKafkaProducer> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BrokerAddress,
            Acks = Acks.All,
            EnableDeliveryReports = true,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
        EnsureTopicsExist();
    }

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topic, string message)
    {
        try
        {
            DeliveryResult<Null, string> result = await _producer.ProduceAsync(topic, new Message<Null, string>
            {
                Value = message
            });

            if (result.Status != PersistenceStatus.Persisted)
            {
                _logger.LogError("// KafkaProducer // ProduceAsync // Message not ack'd by all brokers (value: '{message}'). Delivery status: {result.Status}", message, result.Status);
                return false;
            }
        }
        catch (ProduceException<long, string> ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Permanent error: {Message} for message (value: '{DeliveryResult}')", ex.Message, ex.DeliveryResult.Value);
            throw;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteTopicAsync(string topic)
    {
        using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _settings.BrokerAddress } }).Build();
        try
        {
            await adminClient.DeleteTopicsAsync(new string[] { topic });
            return true;
        }
        catch (DeleteTopicsException ex)
        {
            _logger.LogError(ex, "// KafkaProducer // DeleteTopic // Failed to delete topic '{Topic}'", topic);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Flushs the producer
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        _producer.Flush();
    }

    private void EnsureTopicsExist()
    {
        using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _settings.BrokerAddress } }).Build();
        var existingTopics = adminClient.GetMetadata(TimeSpan.FromSeconds(10)).Topics;

        foreach (string topic in _settings.TopicList)
        {
            if (!existingTopics.Exists(t => t.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    adminClient.CreateTopicsAsync(new TopicSpecification[]
                    {
                        new TopicSpecification
                        {
                            Name = topic,
                            NumPartitions = 1, // Set the desired number of partitions
                            ReplicationFactor = 1 // Set the desired replication factor
                        }
                    }).Wait();
                    _logger.LogInformation("// KafkaProducer // EnsureTopicsExists // Topic '{Topic}' created successfully.", topic);
                }
                catch (CreateTopicsException ex)
                {
                    _logger.LogError(ex, "// KafkaProducer // EnsureTopicsExists // Failed to create topic '{Topic}'", topic);
                    throw;
                }
            }
        }
    }
}