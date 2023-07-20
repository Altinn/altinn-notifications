namespace Altinn.Notifications.Core.Configuration;

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
    /// The name of the past due orders topic 
    /// </summary>
    public string PastDueOrdersTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the general email queue topic
    /// </summary>
    public string EmailQueueTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The group id for all consumers of the Altinn Notifications service
    /// </summary>
    public string ConsumerGroupId { get; set; } = string.Empty;
}