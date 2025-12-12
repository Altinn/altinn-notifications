using Altinn.Notifications.Integrations.Configuration;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Tools;

/// <summary>
/// Container class for configuration objects used by producers and consumers. Will also help with
/// initialization of some default kafkaSettings common across all Kafka clients.
/// </summary>
public class SharedClientConfig
{
    /// <summary>
    /// Admin client configuration to use for kafka admin
    /// </summary>
    public AdminClientConfig AdminClientConfig { get; }

    /// <summary>
    /// Generic client configuration to use for kafka producer
    /// </summary>
    public ClientConfig ProducerConfig { get; }

    /// <summary>
    /// Generic client configuration to use for kafka consumer 
    /// </summary>
    public ClientConfig ConsumerConfig { get; }

    /// <summary>
    /// TopicSpecification
    /// </summary>
    public TopicSpecification TopicSpecification { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedClientConfig"/> class.
    /// </summary>
    public SharedClientConfig(KafkaSettings kafkaSettings)
    {
        var adminConfig = new AdminClientConfig()
        {
            BootstrapServers = kafkaSettings.BrokerAddress,
        };

        var producerConfig = new ClientConfig
        {
            BootstrapServers = kafkaSettings.BrokerAddress,
        };

        var consumerConfig = new ClientConfig
        {
            BootstrapServers = kafkaSettings.BrokerAddress
        };

        var topicSpec = new TopicSpecification()
        {
            NumPartitions = 1,
            ReplicationFactor = 1
        };

        if (!string.IsNullOrEmpty(kafkaSettings.Admin.SaslUsername) && !string.IsNullOrEmpty(kafkaSettings.Admin.SaslPassword))
        {
            adminConfig.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            adminConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            adminConfig.SaslMechanism = SaslMechanism.Plain;
            adminConfig.SaslUsername = kafkaSettings.Admin.SaslUsername;
            adminConfig.SaslPassword = kafkaSettings.Admin.SaslPassword;

            producerConfig.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            producerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            producerConfig.SaslMechanism = SaslMechanism.Plain;
            producerConfig.SaslUsername = kafkaSettings.Producer.SaslUsername;
            producerConfig.SaslPassword = kafkaSettings.Producer.SaslPassword;

            consumerConfig.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            consumerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            consumerConfig.SaslMechanism = SaslMechanism.Plain;
            consumerConfig.SaslUsername = kafkaSettings.Consumer.SaslUsername;
            consumerConfig.SaslPassword = kafkaSettings.Consumer.SaslPassword;

            string retentionTime = kafkaSettings.Admin.RetentionTime < 0 ? "-1" : TimeSpan.FromDays(kafkaSettings.Admin.RetentionTime).TotalMilliseconds.ToString();
            topicSpec.NumPartitions = 6;
            topicSpec.ReplicationFactor = 3;
            topicSpec.Configs = new Dictionary<string, string>()
                                {
                                    { "retention.ms", retentionTime },
                                    { "cleanup.policy", "delete" }
                                };
        }

        AdminClientConfig = adminConfig;
        ProducerConfig = producerConfig;
        ConsumerConfig = consumerConfig;
        TopicSpecification = topicSpec;
    }
}
