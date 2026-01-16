using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.Extensions.Logging;

namespace Tools.Kafka;

/// <summary>
/// Implementation of a generic Kafka producer.
/// </summary>
public sealed class CommonProducer : ICommonProducer, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaSettings _kafkaSettings;
    private readonly SharedClientConfig _sharedClientConfig;
    private readonly ILogger<CommonProducer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ICommonProducer"/> class.
    /// </summary>
    public CommonProducer(KafkaSettings kafkaSettings, ILogger<CommonProducer> logger)
    {
        _kafkaSettings = kafkaSettings;
        _logger = logger;

        _sharedClientConfig = new SharedClientConfig(kafkaSettings);

        var config = new ProducerConfig(_sharedClientConfig.ProducerSettings)
        {
            Acks = Acks.All,
            EnableDeliveryReports = true,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();

        EnsureTopicsExist();
    }

    /// <summary>
    /// Constructor for injecting a mock producer for unit testing
    /// </summary>
    internal CommonProducer(KafkaSettings kafkaSettings, ILogger<CommonProducer> logger, IProducer<Null, string> producer, SharedClientConfig sharedClientConfig)
    {
        _kafkaSettings = kafkaSettings;
        _logger = logger;
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _sharedClientConfig = sharedClientConfig ?? new SharedClientConfig(kafkaSettings);
        // Do NOT call EnsureTopicsExist() in this constructor - keeps unit tests isolated from Kafka
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
                _logger.LogError("// KafkaProducer // ProduceAsync // Message not ack'd by all brokers (value: '{Message}'). Delivery status: {Status}", message, result.Status);
                return false;
            }
        }
        catch (ProduceException<Null, string> ex)
        {
            var deliveredValue = ex.DeliveryResult?.Value;
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Permanent error: {Message} for message (value: '{DeliveryResult}')", ex.Message, deliveredValue);
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _producer?.Flush();
        _producer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void EnsureTopicsExist()
    {
        var adminClientConfig = new AdminClientConfig(_sharedClientConfig.AdminClientSettings);
        using var adminClient = new AdminClientBuilder(adminClientConfig).Build();

        // Delegate the core logic to the test-friendly overload that accepts delegates.
        EnsureTopicsExist(
            () => adminClient.GetMetadata(TimeSpan.FromSeconds(10)).Topics.Select(t => t.Topic),
            spec => adminClient.CreateTopicsAsync(new TopicSpecification[] { spec }).Wait());
    }

    /// <summary>
    /// Internal overload used to make EnsureTopicsExist testable.
    /// </summary>
    /// <param name="getExistingTopics">Function that returns the existing topic names.</param>
    /// <param name="createTopicAction">Action that will create a given TopicSpecification.</param>
    internal void EnsureTopicsExist(Func<IEnumerable<string>> getExistingTopics, Action<TopicSpecification> createTopicAction)
    {
        var existingTopics = getExistingTopics() ?? Enumerable.Empty<string>();
        var topicsNotExisting = _kafkaSettings.Admin.TopicList.Except(existingTopics, StringComparer.OrdinalIgnoreCase);

        foreach (string topic in topicsNotExisting)
        {
            var spec = new TopicSpecification()
            {
                Name = topic,
                NumPartitions = _sharedClientConfig.TopicSpecification.NumPartitions,
                ReplicationFactor = _sharedClientConfig.TopicSpecification.ReplicationFactor,
                Configs = _sharedClientConfig.TopicSpecification.Configs
            };

            createTopicAction(spec);
            _logger.LogInformation("// KafkaProducer // EnsureTopicsExists // Topic '{Topic}' created successfully.", topic);
        }
    }
}
