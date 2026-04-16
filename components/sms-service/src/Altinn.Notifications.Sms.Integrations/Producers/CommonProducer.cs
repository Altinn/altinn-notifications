using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.Producers;

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

        var config = new ProducerConfig(_sharedClientConfig.ProducerConfig)
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

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topic, string message)
    {
        try
        {
            DeliveryResult<Null, string> result = await _producer.ProduceAsync(topic, new Message<Null, string>
            {
                Value = message
            });

            if (result.Status == PersistenceStatus.Persisted)
            {
                return true;
            }

            _logger.LogError(
                "// CommonProducer // ProduceAsync // Message not ack'd by all brokers. Delivery status: {Status}, Partition: {Partition}, Offset: {Offset}",
                result.Status,
                result.Partition.Value,
                result.Offset.Value);

            return false;
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(ex, "// CommonProducer // ProduceAsync // Permanent error: {ErrorReason}", ex.Error.Reason);

            return false;
        }
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
        using var adminClient = new AdminClientBuilder(_sharedClientConfig.AdminClientConfig).Build();
        var existingTopics = adminClient.GetMetadata(TimeSpan.FromSeconds(10)).Topics;

        var topicsNotExisting = _kafkaSettings.Admin.TopicList.Except(existingTopics.Select(t => t.Topic), StringComparer.OrdinalIgnoreCase);
        foreach (string topic in topicsNotExisting)
        {
            try
            {
                adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification()
                    {
                        Name = topic,
                        NumPartitions = _sharedClientConfig.TopicSpecification.NumPartitions,
                        ReplicationFactor = _sharedClientConfig.TopicSpecification.ReplicationFactor,
                        Configs = _sharedClientConfig.TopicSpecification.Configs
                    }
                ]).Wait();
                _logger.LogInformation("// KafkaProducer // EnsureTopicsExists // Topic created successfully.");
            }
            catch (AggregateException ex) when (ex.InnerException is CreateTopicsException cte
                && cte.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                _logger.LogInformation("// KafkaProducer // EnsureTopicsExists // Topic '{Topic}' already exists, skipping.", topic);
            }
        }
    }
}
