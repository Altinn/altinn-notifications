using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Altinn.Notifications.Email.IntegrationTests.Utils;

public static class KafkaUtil
{
    private const string _brokerAddress = "localhost:9092";

    public static async Task DeleteTopicAsync(string topic)
    {
        using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _brokerAddress } }).Build();
        await adminClient.DeleteTopicsAsync(new string[] { topic });
    }

    public static async Task CreateTopicsAsync(string topic)
    {
        using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _brokerAddress } }).Build();

        await adminClient.CreateTopicsAsync(new TopicSpecification[]
        {
            new TopicSpecification
            {
                Name = topic,
                NumPartitions = 1, // Set the desired number of partitions
                ReplicationFactor = 1 // Set the desired replication factor
            }
        });
    }

    public static async Task PostMessage(string topic, string message)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _brokerAddress,
            Acks = Acks.All,
            EnableDeliveryReports = true,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();

        DeliveryResult<Null, string> result = await producer.ProduceAsync(topic, new Message<Null, string>
        {
            Value = message
        });

        if (result.Status != PersistenceStatus.Persisted)
        {
            throw new Exception($"Non positive result: {result.Status}");
        }
    }
}
