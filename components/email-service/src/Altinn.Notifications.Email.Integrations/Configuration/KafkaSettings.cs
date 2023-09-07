namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Configuration object used to hold integration settings for a Kafka.
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
    /// The name of the email sending accepted topic
    /// </summary>
    public string EmailSendingAcceptedTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the send email queue topic
    /// </summary>
    public string SendEmailQueueTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the send email queue retry  topic
    /// </summary>    
    public string SendEmailQueueRetryTopicName { get; set; } = string.Empty;
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
}
