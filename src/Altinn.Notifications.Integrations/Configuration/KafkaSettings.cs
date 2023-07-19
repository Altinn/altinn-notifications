namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration object used to hold settings for a kafka integration settings.
/// </summary>
public class KafkaSettings
{
    /// <summary>
    /// The address of the kafka broker
    /// </summary>
    public string BrokerAddress { get; set; } = string.Empty;

    /// <summary>
    /// List of topics
    /// </summary>
    public List<string> TopicList { get; set; } = new List<string>();
}