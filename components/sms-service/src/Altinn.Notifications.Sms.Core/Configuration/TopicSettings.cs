namespace Altinn.Notifications.Sms.Core.Configuration;

/// <summary>
/// Configuration object used to hold topic names for core services to publish to in Kafka.
/// </summary>
public class TopicSettings
{
    /// <summary>
    /// The name of the sms status updated topic
    /// </summary>    
    public string SmsStatusUpdatedTopicName { get; set; } = string.Empty;
}
