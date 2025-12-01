using Altinn.Notifications.Integrations.Kafka.Producers;

using Confluent.Kafka;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingProducer;

/// <summary>
/// Test‑only Kafka producer stub that forces failures for all produce operations.
/// Used to exercise the batch failure fallback logic in <see cref="KafkaProducer"/>.
/// </summary>
internal sealed class FailingProducer : IProducer<Null, string>
{
    /// <summary>
    /// Not supported in this test stub. Accessing this property indicates
    /// a test invoked functionality outside the intended scope.
    /// </summary>
    public Handle Handle => throw new NotSupportedException("Handle not available for test stub.");

    /// <summary>
    /// Gets the logical name of the test stub producer.
    /// </summary>
    public string Name => "FailingProducer";

    /// <summary>
    /// Adds brokers to the internal list. Not needed for tests; returns 0.
    /// </summary>
    public int AddBrokers(string brokers) => 0;

    /// <summary>
    /// Flushes outstanding messages. No‑op for the throwing stub.
    /// </summary>
    public static void Flush()
    {
        // No operation needed for the stub.
    }

    /// <summary>
    /// Polls internal events. Always returns 0 for the stub.
    /// </summary>
    public int Poll(TimeSpan timeout) => 0;

    /// <summary>
    /// Disposes the stub producer. No resources to release.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Initializes transactions. No‑op in stub.
    /// </summary>
    public void InitTransactions(TimeSpan timeout)
    {
    }

    /// <summary>
    /// Begins a transaction. No‑op in stub.
    /// </summary>
    public void BeginTransaction()
    {
    }

    /// <summary>
    /// Commits a transaction without timeout. Not supported.
    /// </summary>
    public void CommitTransaction()
    {
        throw new NotSupportedException("CommitTransaction() not used in tests.");
    }

    /// <summary>
    /// Commits a transaction with a timeout. No‑op in stub.
    /// </summary>
    public void CommitTransaction(TimeSpan timeout)
    {
    }

    /// <summary>
    /// Aborts a transaction without timeout. Not supported.
    /// </summary>
    public void AbortTransaction() => throw new NotSupportedException("AbortTransaction() not used in tests.");

    /// <summary>
    /// Aborts a transaction with a timeout. No‑op in stub.
    /// </summary>
    public void AbortTransaction(TimeSpan timeout) => throw new NotSupportedException("AbortTransaction() not used in tests.");

    /// <summary>
    /// Sends offsets to the current transaction. No‑op in stub.
    /// </summary>
    public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
    {
        // No operation needed for the stub.
    }

    /// <summary>
    /// Asynchronous produce that always returns a faulted task to simulate failure.
    /// </summary>
    /// <returns>A faulted task containing a simulated produce exception.</returns>
    public Task<DeliveryResult<Null, string>> ProduceAsync(string topic, Message<Null, string> message, CancellationToken cancellationToken = default) => Task.FromException<DeliveryResult<Null, string>>(new Exception("Simulated produce failure (ProduceAsync)"));

    /// <summary>
    /// Asynchronous partition-specific produce. Not implemented for tests.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public Task<DeliveryResult<Null, string>> ProduceAsync(TopicPartition topicPartition, Message<Null, string> message, CancellationToken cancellationToken = default) => throw new NotSupportedException("Partition-specific ProduceAsync not used in tests.");

    /// <summary>
    /// Synchronous produce that always throws to simulate a failure before acknowledgment.
    /// </summary>
    /// <exception cref="Exception">Always thrown to force failure path.</exception>
    public void Produce(string topic, Message<Null, string> message, Action<DeliveryReport<Null, string>>? deliveryHandler = default) => throw new Exception("Simulated produce failure (Produce)");

    /// <summary>
    /// Synchronous partition-specific produce. Not implemented for tests.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public void Produce(TopicPartition topicPartition, Message<Null, string> message, Action<DeliveryReport<Null, string>>? deliveryHandler = null) => throw new NotSupportedException("Partition-specific Produce not used in tests.");

    /// <summary>
    /// Flush implementation required by explicit interface. Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown if invoked.</exception>
    int IProducer<Null, string>.Flush(TimeSpan timeout) => throw new NotSupportedException("Explicit Flush not used in tests.");

    /// <summary>
    /// Flush with cancellation token. Not supported.
    /// </summary>
    public void Flush(CancellationToken cancellationToken = default) => throw new NotSupportedException("Flush with CancellationToken not used in tests.");

    /// <summary>
    /// Sets SASL credentials for authentication. Not supported in this stub.
    /// </summary>
    public void SetSaslCredentials(string username, string password) => throw new NotSupportedException("Authentication not used in tests.");
}
