namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration object used to hold integration settings for a Kafka.
/// </summary>
public class KafkaSettings
{
    /// <summary>
    /// The name of the past due orders topic
    /// </summary>
    public string PastDueOrdersTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the general email queue topic
    /// </summary>
    public string EmailQueueTopicName { get; set; } = string.Empty;

    /// <summary>
    /// THe name of the general sms queue topic
    /// </summary>
    public string SmsQueTopicName { get; set; } = string.Empty;
}
