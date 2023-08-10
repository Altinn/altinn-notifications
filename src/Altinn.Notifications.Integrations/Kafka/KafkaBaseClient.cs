using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Altinn.Notifications.Integrations.Kafka;

/// <summary>
/// Base class
/// </summary>
public abstract class KafkaBaseClient
{
    /// <summary>
    /// Generic client configuration to use for kafka producer, consumer and admin
    /// </summary>
    public ClientConfig ClientConfig { get; }

    /// <summary>
    /// TopicSpecification
    /// </summary>
    public TopicSpecification TopicSpecification { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaBaseClient"/> class.
    /// </summary>
    protected KafkaBaseClient(KafkaSettings settings)
    {
        bool isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        var config = new ClientConfig
        {
            BootstrapServers = settings.BrokerAddress,
        };

        var topicSpec = new TopicSpecification()
        {
            NumPartitions = 1,
            ReplicationFactor = 1
        };

        if (!isDevelopment)
        {
            config.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            config.SecurityProtocol = SecurityProtocol.SaslSsl;
            config.SaslMechanism = SaslMechanism.Plain;
            config.SaslUsername = settings.SaslUsername;
            config.SaslPassword = settings.SaslPassword;

            topicSpec.NumPartitions = 6;
            topicSpec.ReplicationFactor = 3;
        }

        /*
         
         
                { "bootstrap.servers", _settings.BrokerAddress },
                { "security.protocol", "SASL_SSL" },
                { "sasl.mechanisms", "PLAIN" },
                { "sasl.username", _settings.SaslUsername },
                { "sasl.password", _settings.SaslPassword }*/

        ClientConfig = config;
        TopicSpecification = topicSpec;
    }
}