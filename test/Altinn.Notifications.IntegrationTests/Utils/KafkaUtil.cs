using Confluent.Kafka;

namespace Altinn.Notifications.IntegrationTests.Utils;
public static class KafkaUtil
{
    private const string _brokerAddress = "localhost:9092";

    /// <inheritdoc/>
    public static async Task DeleteTopicAsync(string topic)
    {
        using var adminClient = new AdminClientBuilder(new Dictionary<string, string>() { { "bootstrap.servers", _brokerAddress } }).Build();
        await adminClient.DeleteTopicsAsync(new string[] { topic });
    }
}
