using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Altinn.Notifications.Integrations.Kafka;

/// <summary>
/// Base class
/// </summary>
public class SharedClientConfig
{
    /// <summary>
    /// Admin client configuration to use for kafka admin
    /// </summary>
    public AdminClientConfig AdminClientSettings { get; }

    /// <summary>
    /// Generic client configuration to use for kafka producer
    /// </summary>
    public ClientConfig ProducerSettings { get; }

    /// <summary>
    /// Generic client configuration to use for kafka consumer 
    /// </summary>
    public ClientConfig ConsumerSettings { get; }

    /// <summary>
    /// TopicSpecification
    /// </summary>
    public TopicSpecification TopicSpecification { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedClientConfig"/> class.
    /// </summary>
    public SharedClientConfig(Configuration.KafkaSettings settings)
    {
        var adminConfig = new AdminClientConfig()
        {
            BootstrapServers = settings.BrokerAddress,
            ReconnectBackoffMs = 50,
            ReconnectBackoffMaxMs = 10000
        };

        var producerConfig = new ClientConfig
        {
            BootstrapServers = settings.BrokerAddress,
            ReconnectBackoffMs = 50,
            ReconnectBackoffMaxMs = 10000
        };

        var consumerConfig = new ClientConfig
        {
            BootstrapServers = settings.BrokerAddress,
            ReconnectBackoffMs = 50,
            ReconnectBackoffMaxMs = 10000
        };

        var topicSpec = new TopicSpecification()
        {
            NumPartitions = 1,
            ReplicationFactor = 1
        };

        if (!string.IsNullOrEmpty(settings.Admin.SaslUsername) && !string.IsNullOrEmpty(settings.Admin.SaslPassword))
        {
            adminConfig.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            adminConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            adminConfig.SaslMechanism = SaslMechanism.Plain;
            adminConfig.SaslUsername = settings.Admin.SaslUsername;
            adminConfig.SaslPassword = settings.Admin.SaslPassword;

            producerConfig.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            producerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            producerConfig.SaslMechanism = SaslMechanism.Plain;
            producerConfig.SaslUsername = settings.Producer.SaslUsername;
            producerConfig.SaslPassword = settings.Producer.SaslPassword;

            consumerConfig.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            consumerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            consumerConfig.SaslMechanism = SaslMechanism.Plain;
            consumerConfig.SaslUsername = settings.Consumer.SaslUsername;
            consumerConfig.SaslPassword = settings.Consumer.SaslPassword;

            string retentionTime = settings.Admin.RetentionTime < 0 ? "-1" : TimeSpan.FromDays(settings.Admin.RetentionTime).TotalMilliseconds.ToString();
            topicSpec.NumPartitions = 6;
            topicSpec.ReplicationFactor = 3;
            topicSpec.Configs = new Dictionary<string, string>()
                                {
                                    { "retention.ms", retentionTime
},
                                    { "cleanup.policy", "delete" }
                                };
        }

        AdminClientSettings = adminConfig;
        ProducerSettings = producerConfig;
        ConsumerSettings = consumerConfig;
        TopicSpecification = topicSpec;
    }
}
