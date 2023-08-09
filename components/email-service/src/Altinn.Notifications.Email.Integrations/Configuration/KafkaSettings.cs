using Altinn.Notifications.Email.Integrations.Consumers;

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
    /// Settings specific for the <see cref="EmailSendingConsumer"/> consumer.
    /// </summary>
    public EmailSendingConsumerSettings EmailSendingConsumerSettings { get; set; } = new();
}

/// <summary>
/// Configuration object for the <see cref="EmailSendingConsumer"/>.
/// </summary>
public class EmailSendingConsumerSettings
{
    /// <summary>
    /// The group id for all consumers of the Altinn Notifications service
    /// </summary>
    public string ConsumerGroupId { get; set; } = string.Empty;
    
    /// <summary>
    /// The name of the past due orders topic 
    /// </summary>
    public string TopicName { get; set; } = string.Empty;
}
