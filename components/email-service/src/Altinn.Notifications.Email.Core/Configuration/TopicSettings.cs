namespace Altinn.Notifications.Email.Core.Configuration;

/// <summary>
/// Configuration object used to hold topic names for core services to publish to in Kafka.
/// </summary>
public class TopicSettings
{
    /// <summary>
    /// The name of the email sending accepted topic
    /// </summary>
    public string EmailSendingAcceptedTopicName { get; set; } = string.Empty;
    
    /// <summary>
    /// The name of the email status updated topic
    /// </summary>    
    public string EmailStatusUpdatedTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the send email queue retry  topic
    /// </summary>    
    public string SendEmailQueueRetryTopicName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the Altinn service update topic
    /// </summary>
    public string AltinnServiceUpdateTopicName { get; set; } = string.Empty;
}
