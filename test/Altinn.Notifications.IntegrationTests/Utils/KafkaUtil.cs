using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Altinn.Notifications.IntegrationTests.Utils;
public static class KafkaUtil
{
    private const string _brokerAddress = "localhost:9092";

    /// <inheritdoc/>
    public static async Task DeleteTopicAsync(string topic)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _brokerAddress } }).Build();
            await adminClient.DeleteTopicsAsync(new string[] { topic });

        }
        catch
        {
            // not critical if topic not deleted
        }
    }

    public static async Task<bool> PublishMessageOnTopic(string topic, string message)
    {
        using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _brokerAddress } }).Build();
        await adminClient.CreateTopicsAsync(new TopicSpecification[]
                   {
                        new TopicSpecification()
                        {
                            Name = topic,
                            NumPartitions = 1,
                            ReplicationFactor = 1
                        }
                   });

        var config = new ProducerConfig()
        {
            BootstrapServers = _brokerAddress,
            Acks = Acks.All,
            EnableDeliveryReports = true
        };

        using var _producer = new ProducerBuilder<Null, string>(config).Build();
        var r = await _producer.ProduceAsync(topic, new Message<Null, string>
        {
            Value = message
        });


        return r.Status == PersistenceStatus.Persisted;
    }
}
