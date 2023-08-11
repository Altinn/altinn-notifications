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
    /// The sasl username
    /// </summary>
    public string SaslUsername { get; set; } = string.Empty;

    /// <summary>
    /// The sasl password
    /// </summary>
    public string SaslPassword { get; set; } = string.Empty;

    /// <summary>
    /// List of topics
    /// </summary>
    public List<string> TopicList { get; set; } = new List<string>();

    /// <summary>
    /// The name of the past due orders topic
    /// </summary>
    public string PastDueOrdersTopicName { get; set; } = string.Empty;
    
    /// <summary>
    /// The name of the past due orders retry topic
    /// </summary>
    public string PastDueOrdersTopicNameRetry { get; set; } = string.Empty;

    /// <summary>
    /// The name of the general email queue topic
    /// </summary>
    public string PastDueOrdersTopicNameRetry { get; set; } = string.Empty;

    /// <summary>
    /// The name of the general email queue topic
    /// </summary>
    public string EmailQueueTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the health check topic
    /// </summary>
    public string HealthCheckTopic { get; set; } = string.Empty;

    /// <summary>
    /// The group id for all consumers of the Altinn Notifications service
    /// </summary>
    public string ConsumerGroupId { get; set; } = string.Empty;
}