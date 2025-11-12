using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.Kafka.Extensions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Implementation of a Kafka producer
/// </summary>
public class KafkaProducer : SharedClientConfig, IKafkaProducer, IDisposable
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly IProducer<Null, string> _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaProducer"/> class.
    /// </summary>
    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
        : base(settings.Value)
    {
        _logger = logger;
        _settings = settings.Value;

        var config = new ProducerConfig(ProducerSettings)
        {
            LingerMs = 5,
            Acks = Acks.All,
            MaxInFlight = 5,
            RetryBackoffMs = 1000,
            BatchSize = 128 * 1024,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            EnableBackgroundPoll = true,
            SocketKeepaliveEnable = true,
            EnableDeliveryReports = true,
            CompressionType = CompressionType.Lz4
        };

        _producer = new ProducerBuilder<Null, string>(config)
                   .BuildWithInstrumentation();

        EnsureTopicsExist();
    }

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topic, string message)
    {
        try
        {
            DeliveryResult<Null, string> result = await _producer.ProduceAsync(topic, new Message<Null, string>
            {
                Value = message
            });

            if (result.Status != PersistenceStatus.Persisted)
            {
                _logger.LogError("// KafkaProducer // ProduceAsync // Message not ack'd by all brokers (value: '{Message}'). Delivery status: {Status}", message, result.Status);
                return false;
            }
        }
        catch (ProduceException<long, string> ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Permanent error: {Message} for message (value: '{DeliveryResult}')", ex.Message, ex.DeliveryResult.Value);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // An exception occurred.");
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topic, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _producer.ProduceAsync(
                topic,
                new Message<Null, string> { Value = message },
                cancellationToken).ConfigureAwait(false);

            if (result.Status != PersistenceStatus.Persisted)
            {
                _logger.LogError("// KafkaProducer // ProduceAsync // Not persisted (status: {Status}) value: '{Message}'", result.Status, message);

                return false;
            }

            return true;
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(
                ex,
                "// KafkaProducer // ProduceAsync // ProduceException (code: {Code}) value: '{Value}'",
                ex.Error.Code,
                ex.DeliveryResult?.Value);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("// KafkaProducer // ProduceAsync // Cancelled producing to topic {Topic}", topic);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Unexpected exception.");

            return false;
        }
    }

    /// <summary>
    /// Produces a batch of messages with per-message delivery awaits.
    /// Prefer when you already have multiple messages and want fewer async state machine transitions.
    /// </summary>
    /// <param name="topic">Kafka topic name.</param>
    /// <param name="messages">Collection of message payloads.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of messages successfully persisted.</returns>
    public async Task<int> ProduceBatchAsync(string topic, IEnumerable<string> messages, CancellationToken cancellationToken = default)
    {
        int successCount = 0;

        // Materialize to avoid multiple enumeration
        var list = messages as IList<string> ?? messages.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        // Issue all produce tasks concurrently; the producer client batches internally
        var tasks = list.Select(m => _producer.ProduceAsync(
            topic,
            new Message<Null, string> { Value = m },
            cancellationToken));

        try
        {
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var r in results)
            {
                if (r.Status == PersistenceStatus.Persisted)
                {
                    successCount++;
                }
                else
                {
                    _logger.LogError("// KafkaProducer // ProduceBatchAsync // Not persisted (status: {Status}) value: '{Value}'", r.Status, r.Value);
                }
            }
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(
                ex,
                "// KafkaProducer // ProduceBatchAsync // ProduceException (code: {Code}) value: '{Value}'",
                ex.Error.Code,
                ex.DeliveryResult?.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceBatchAsync // Unexpected exception.");
        }

        return successCount;
    }

    /// <summary>
    /// Disposes the producer instance, attempting a graceful flush with timeout.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose pattern implementation.
    /// </summary>
    /// <param name="disposing">True when called from <see cref="Dispose"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        try
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "// KafkaProducer // Dispose // Flush timeout or error.");
        }
        finally
        {
            _producer.Dispose();
        }
    }

    /// <summary>
    /// Ensures required topics exist by creating any missing topics using the admin client.
    /// Intended to run at startup; avoid calling on the hot path of producing.
    /// </summary>
    private void EnsureTopicsExist()
    {
        using var adminClient = new AdminClientBuilder(AdminClientSettings).Build();
        Metadata metadata;
        try
        {
            metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // EnsureTopicsExist // Failed to fetch metadata.");
            throw;
        }

        var existingNames = new HashSet<string>(metadata.Topics.Select(t => t.Topic), StringComparer.OrdinalIgnoreCase);

        foreach (string topic in _settings.Admin.TopicList)
        {
            if (existingNames.Contains(topic))
            {
                continue;
            }

            try
            {
                adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = topic,
                        Configs = TopicSpecification.Configs,
                        NumPartitions = TopicSpecification.NumPartitions,
                        ReplicationFactor = TopicSpecification.ReplicationFactor
                    }
                ]).GetAwaiter().GetResult();

                _logger.LogInformation("// KafkaProducer // EnsureTopicsExist // Created topic '{Topic}'", topic);
            }
            catch (CreateTopicsException ex)
            {
                // If topic already exists (race), treat as success.
                if (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    _logger.LogInformation("// KafkaProducer // EnsureTopicsExist // Topic '{Topic}' already exists (race).", topic);
                    continue;
                }

                _logger.LogError(ex, "// KafkaProducer // EnsureTopicsExist // Failed to create topic '{Topic}'", topic);
                throw;
            }
        }
    }
}
