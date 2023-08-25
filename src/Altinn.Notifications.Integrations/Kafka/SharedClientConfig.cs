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
        };

        var producerConfig = new ClientConfig
        {
            BootstrapServers = settings.BrokerAddress,
        };

        var consumerConfig = new ClientConfig
        {
            BootstrapServers = settings.BrokerAddress
        };

        var topicSpec = new TopicSpecification()
        {
            NumPartitions = 1,
            ReplicationFactor = 1
        };

        if (!string.IsNullOrEmpty(settings.Admin.SaslUsername) && !string.IsNullOrEmpty(settings.Admin.SaslPassword))
        {
            Console.WriteLine("// SASL registered and being set");

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

            topicSpec.NumPartitions = 6;
            topicSpec.ReplicationFactor = 3;
        }

        AdminClientSettings = adminConfig;
        ProducerSettings = producerConfig;
        ConsumerSettings = consumerConfig;
        TopicSpecification = topicSpec;
    }
}