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
    private readonly int _maxBatchSize = 75;
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
            LingerMs = 100,
            Acks = Acks.All,
            MaxInFlight = 5,
            RetryBackoffMs = 1000,
            BatchSize = 512 * 1024,
            BatchNumMessages = 1000,
            EnableIdempotence = true,
            MessageSendMaxRetries = 5,
            EnableBackgroundPoll = true,
            SocketKeepaliveEnable = true,
            EnableDeliveryReports = true,
            DeliveryReportFields = "status",
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
    public async Task<IEnumerable<string>> ProduceAsync(string topic, IEnumerable<string> messages, CancellationToken cancellationToken = default)
    {
        var messagesToPublish = messages as IList<string> ?? [.. messages];
        if (messagesToPublish.Count == 0)
        {
            return [];
        }

        var successfullMessagesCount = 0;
        var failedMessages = new List<string>();
        for (int i = 0; i < messagesToPublish.Count; i += _maxBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messagesSegment = messagesToPublish.Skip(i).Take(_maxBatchSize).ToList();

            var publishingTasks = messagesSegment.Select((message, index) => new
            {
                Index = index,
                Message = message,
                Task = _producer.ProduceAsync(topic, new Message<Null, string> { Value = message }, cancellationToken)
            }).ToArray();

            try
            {
                var publishingTasksResults = await Task.WhenAll(publishingTasks.Select(e => e.Task)).ConfigureAwait(false);
                for (int x = 0; x < publishingTasksResults.Length; x++)
                {
                    var publishingResult = publishingTasksResults[x];
                    if (publishingResult.Status != PersistenceStatus.Persisted)
                    {
                        failedMessages.Add(publishingTasks[x].Message);
                    }
                    else
                    {
                        successfullMessagesCount++;
                    }
                }
            }
            catch (ProduceException<Null, string> ex)
            {
                _logger.LogError(ex, "// KafkaProducer // ProduceBatchAsync // ProduceException (code: {Code}) value: '{Value}'", ex.Error.Code, ex.DeliveryResult?.Value);

                if (ex.DeliveryResult?.Value != null)
                {
                    failedMessages.Add(ex.DeliveryResult.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// KafkaProducer // ProduceBatchAsync // Unexpected exception in chunk {ChunkStart}-{ChunkEnd}", i, Math.Min(i + _maxBatchSize - 1, messagesToPublish.Count - 1));

                failedMessages.AddRange(messagesSegment);
            }
        }

        _logger.LogInformation(
            "// KafkaProducer // ProduceBatchAsync // Batch publishing completed for topic {Topic}. Total messages: {TotalMessages}, Successfully published: {SuccessCount}, Failed: {FailedCount}, Success rate: {SuccessRate:P2}",
            topic,
            messagesToPublish.Count,
            successfullMessagesCount,
            failedMessages.Count,
            successfullMessagesCount / messagesToPublish.Count);

        return failedMessages;
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
            _logger.LogError(ex, "// KafkaProducer // Dispose // Flush timeout or error.");
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
