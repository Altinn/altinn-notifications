using Altinn.Notifications.Integrations.Configuration;

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
    public AdminClientConfig AdminClientConfig { get; }

    /// <summary>
    /// Generic client configuration to use for kafka producer and consumer 
    /// </summary>
    public ClientConfig ClientConfig { get; }

    /// <summary>
    /// TopicSpecification
    /// </summary>
    public TopicSpecification TopicSpecification { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedClientConfig"/> class.
    /// </summary>
    public SharedClientConfig(KafkaSettings settings)
    {
        bool isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        var adminConfig = new AdminClientConfig()
        {
            BootstrapServers = settings.BrokerAddress,
        };

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
            adminConfig.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            adminConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            adminConfig.SaslMechanism = SaslMechanism.Plain;
            adminConfig.SaslUsername = settings.SaslUsername;
            adminConfig.SaslPassword = settings.SaslPassword;

            config.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
            config.SecurityProtocol = SecurityProtocol.SaslSsl;
            config.SaslMechanism = SaslMechanism.Plain;
            config.SaslUsername = settings.SaslUsername;
            config.SaslPassword = settings.SaslPassword;

            topicSpec.NumPartitions = 6;
            topicSpec.ReplicationFactor = 3;
        }

        AdminClientConfig = adminConfig;
        ClientConfig = config;
        TopicSpecification = topicSpec;
    }
}