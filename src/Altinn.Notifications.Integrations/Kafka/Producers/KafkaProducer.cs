using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Implementation of a kafka producer
/// </summary>
public class KafkaProducer : IKafkaProducer
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaProducer"/> class.
    /// </summary>
    public KafkaProducer(IOptions<KafkaSettings> settings)
    {
        _settings = settings.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BrokerAddress
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
        EnsureTopicsExist();
    }

    /// <inheritdoc/>
    public async Task ProduceAsync(string topic, string message)
    {
        var result = await _producer.ProduceAsync(topic, new Message<Null, string>
        {
            Value = message
        });

        Console.WriteLine($"Message sent (key: {result.Key}, value: {result.Value})");
    }

    private void EnsureTopicsExist()
    {
        using (var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _settings.BrokerAddress } }).Build())
        {
            var existingTopics = adminClient.GetMetadata(TimeSpan.FromSeconds(10)).Topics;

            foreach (var topic in _settings.TopicList)
            {
                if (!existingTopics.Any(t => t.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)))
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
                        Console.WriteLine($"Topic '{topic}' created successfully.");
                    }
                    catch (CreateTopicsException ex)
                    {
                        Console.WriteLine($"Failed to create topic '{topic}': {ex.Results[0].Error.Reason}");
                    }
                }
                else
                {
                    Console.WriteLine($"Topic '{topic}' already exists.");
                }
            }
        }
    }
}