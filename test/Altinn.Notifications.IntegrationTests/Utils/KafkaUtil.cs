using Confluent.Kafka;

using Confluent.Kafka.Admin;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class KafkaUtil
{
    private const string _brokerAddress = "localhost:9092";

    /// <summary>
    /// Publishes a message to the specified Kafka topic.
    /// </summary>
    /// <param name="topic">The name of the topic to publish to.</param>
    /// <param name="message">The message content to publish.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topic is null or empty, or message is null.</exception>
    public static async Task PublishMessageOnTopic(string topic, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            using var producer = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = _brokerAddress }).Build();

            var result = await producer.ProduceAsync(topic, new Message<Null, string> { Value = message });

            if (result.Status != PersistenceStatus.Persisted)
            {
                throw new ProduceException<Null, string>(new Error(ErrorCode.Local_Transport, $"Message not persisted to topic '{topic}'. Status: {result.Status}"), result);
            }
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ProduceException<Null, string>(new Error(ErrorCode.Local_Transport, $"Failed to publish message to topic '{topic}'"), null, ex);
        }
    }

    /// <summary>
    /// Deletes a Kafka topic with the specified name.
    /// </summary>
    /// <param name="topic">The name of the topic to delete.</param>
    /// <param name="timeoutMs">Timeout in milliseconds for the operation. Defaults to 10000 (10 seconds).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topic is null or empty.</exception>
    /// <exception cref="KafkaException">Thrown when a Kafka-related error occurs during topic deletion.</exception>
    public static async Task DeleteTopicAsync(string topic, int timeoutMs = 10000)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);

        using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _brokerAddress }).Build();
        try
        {
            await adminClient.DeleteTopicsAsync([topic], new DeleteTopicsOptions { OperationTimeout = TimeSpan.FromMilliseconds(timeoutMs) });
        }
        catch (DeleteTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.UnknownTopicOrPart))
        {
            // Topic doesn't exist - this is fine for testing
        }
    }

    /// <summary>
    /// Creates a Kafka topic with the specified name if it doesn't already exist.
    /// </summary>
    /// <param name="topicName">The name of the topic to create.</param>
    /// <param name="timeoutMs">Timeout in milliseconds for the operation. Defaults to 10000 (10 seconds).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topicName is null or empty.</exception>
    /// <exception cref="KafkaException">Thrown when a Kafka-related error occurs during topic creation.</exception>
    public static async Task CreateTopicAsync(string topicName, int timeoutMs = 10000)
    {
        ArgumentException.ThrowIfNullOrEmpty(topicName);

        using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _brokerAddress }).Build();

        try
        {
            await adminClient.CreateTopicsAsync(
                [new TopicSpecification
                {
                    Name = topicName,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
                ],
                new CreateTopicsOptions { OperationTimeout = TimeSpan.FromMilliseconds(timeoutMs) });
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Topic already exists - this is fine for testing
        }
    }
}
