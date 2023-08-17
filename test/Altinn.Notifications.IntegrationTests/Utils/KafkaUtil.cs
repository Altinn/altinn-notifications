using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Integrations.Kafka.Producers;

using Confluent.Kafka;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class KafkaUtil
{
    private const string _brokerAddress = "localhost:9092";

    public static async Task DeleteTopicAsync(string topic)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _brokerAddress } }).Build();
            await adminClient.DeleteTopicsAsync(new string[] { topic });
        }
        catch (Exception)
        {
            // no biggie if topic is not deleted
        }
    }

    public static async Task PublishMessageOnTopic(string topic, string message)
    {
        var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IKafkaProducer) });
        KafkaProducer producerService = (KafkaProducer)serviceList.First(i => i.GetType() == typeof(KafkaProducer));

        await producerService.ProduceAsync(topic, message);
    }
}