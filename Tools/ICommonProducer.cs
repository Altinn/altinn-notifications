namespace Tools;

/// <summary>
/// This interface describes the minimum requirements for a Kafka producer 
/// </summary>
public interface ICommonProducer
{
    /// <summary>
    /// Produces a message on the given topic
    /// </summary>
    /// <param name="topic">The topic to be written to</param>
    /// <param name="message">The message to write to the topic</param>
    public Task<bool> ProduceAsync(string topic, string message);
}
