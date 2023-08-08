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
}