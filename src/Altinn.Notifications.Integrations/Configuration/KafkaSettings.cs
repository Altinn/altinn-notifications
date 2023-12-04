namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration object used to hold settings for a Kafka integration settings.
/// </summary>
public class KafkaSettings
{
    /// <summary>
    /// The address of the Kafka broker
    /// </summary>
    public string BrokerAddress { get; set; } = string.Empty;

    /// <summary>
    /// The consumer settings
    /// </summary>
    public ConsumerSettings Consumer { get; set; } = new();

    /// <summary>
    /// The producer settings
    /// </summary>
    public ProducerSettings Producer { get; set; } = new();

    /// <summary>
    /// The producer settings
    /// </summary>
    public AdminSettings Admin { get; set; } = new();

    /// <summary>
    /// The name of the past due orders topic
    /// </summary>
    public string PastDueOrdersTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the past due orders retry topic
    /// </summary>
    public string PastDueOrdersRetryTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the general email queue topic
    /// </summary>
    public string EmailQueueTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the email status updated topic
    /// </summary>
    public string EmailStatusUpdatedTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the platform service update topic 
    /// </summary>
    public string AltinnServiceUpdateTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the health check topic
    /// </summary>
    public string HealthCheckTopic { get; set; } = string.Empty;
}

/// <summary>
/// Kafka Consumer specific settings
/// </summary>
public class ConsumerSettings
{
    /// <summary>
    /// The group id for all consumers of the Altinn Notifications service
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// The SASL username
    /// </summary>
    public string SaslUsername { get; set; } = string.Empty;

    /// <summary>
    /// The SASL password
    /// </summary>
    public string SaslPassword { get; set; } = string.Empty;
}

/// <summary>
/// Kafka Producer specific settings
/// </summary>
public class ProducerSettings
{
    /// <summary>
    /// The SASL username
    /// </summary>
    public string SaslUsername { get; set; } = string.Empty;

    /// <summary>
    /// The SASL password
    /// </summary>
    public string SaslPassword { get; set; } = string.Empty;
}

/// <summary>
/// Kafka admin client specific settings
/// </summary>
public class AdminSettings
{
    /// <summary>
    /// The SASL username
    /// </summary>
    public string SaslUsername { get; set; } = string.Empty;

    /// <summary>
    /// The SASL password
    /// </summary>
    public string SaslPassword { get; set; } = string.Empty;

    /// <summary>
    /// The list of topics the admin client is responsible for ensuring that exist
    /// </summary>
    public List<string> TopicList { get; set; } = new List<string>();

    /// <summary>
    /// The retention time in days for the topics
    /// </summary>
    public int RetentionTime { get; set; } = 7;
}
