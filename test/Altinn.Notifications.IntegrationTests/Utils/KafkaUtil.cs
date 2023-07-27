using Confluent.Kafka;

namespace Altinn.Notifications.IntegrationTests.Utils;
public static class KafkaUtil
{
    private const string BrokerAddress = "localhost:9092";

    /// <inheritdoc/>
    public static async Task DeleteTopicAsync(string topic)
    {
        using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", BrokerAddress } }).Build();
        await adminClient.DeleteTopicsAsync(new string[] { topic });
    }
}
