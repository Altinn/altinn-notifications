namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration object used to hold settings for a kafka integration settings.
/// </summary>
public class KafkaTopicSettings
{
    /// <summary>
    /// The name of the past due orders topic 
    /// </summary>
    public string PastDueOrdersTopicName { get; set; } = string.Empty;
}