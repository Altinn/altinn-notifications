namespace Altinn.Notifications.Core.Integrations.Interfaces;

/// <summary>
/// Interface for handeling all producer actions for kafka
/// </summary>
public interface IKafkaProducer
{
    /// <summary>
    /// Produces a message on the provided kafka topic 
    /// </summary>
    /// <param name="topic">The topic to post a message to</param>
    /// <param name="message">The message to post</param>
    public Task<bool> ProduceAsync(string topic, string message);
}