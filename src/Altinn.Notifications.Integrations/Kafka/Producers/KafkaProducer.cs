using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.Kafka.Extensions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Implementation of a Kafka producer.
/// </summary>
public class KafkaProducer : SharedClientConfig, IKafkaProducer, IDisposable
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly IProducer<Null, string> _producer;

    private static readonly Meter _meter = new("Altinn.Notifications.KafkaProducer", "1.0.0");
    private static readonly Counter<int> _failedCounter = _meter.CreateCounter<int>("kafka.producer.failed");
    private static readonly Counter<int> _publishedCounter = _meter.CreateCounter<int>("kafka.producer.published");
    private static readonly Histogram<double> _batchLatencyMs = _meter.CreateHistogram<double>("kafka.producer.batch.latency.ms");
    private static readonly Histogram<double> _singleLatencyMs = _meter.CreateHistogram<double>("kafka.producer.single.latency.ms");

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaProducer"/> class.
    /// </summary>
    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
        : base(settings.Value)
    {
        _logger = logger;
        _settings = settings.Value;

        var config = BuildProducerConfig();

        _producer = BuildProducer(config);

        _ = EnsureTopicsExist();
    }

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topic, string message)
    {
        if (!ValidateTopic(topic))
        {
            return false;
        }

        if (!ValidateMessage(message))
        {
            return false;
        }

        var publishStartTime = Stopwatch.StartNew();

        try
        {
            var result = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = message });

            if (result.Status != PersistenceStatus.Persisted)
            {
                _logger.LogError(
                    "// KafkaProducer // ProduceAsync // Message not persisted. Status={Status} Partition={Partition}",
                    result.Status,
                    result.Partition);

                IncrementFailed();

                return false;
            }

            IncrementPublished();

            return true;
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(
                ex,
                "// KafkaProducer // ProduceAsync // ProduceException Code={Code} Reason={Reason} MessageLength={Length}",
                ex.Error.Code,
                ex.Error.Reason,
                message.Length);

            IncrementFailed();

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Unexpected exception.");

            IncrementFailed();

            return false;
        }
        finally
        {
            publishStartTime.Stop();

            _singleLatencyMs.Record(publishStartTime.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> ProduceAsync(string topic, IEnumerable<string> messages, CancellationToken cancellationToken = default)
    {
        if (!ValidateTopic(topic))
        {
            IncrementFailed(messages.Count());

            return messages;
        }

        var allMessages = messages as ICollection<string> ?? [.. messages];
        List<string> validMessages = [.. allMessages.Where(e => !string.IsNullOrWhiteSpace(e))];

        if (validMessages.Count == 0)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // No valid messages to publish");

            IncrementFailed(allMessages.Count);

            return messages;
        }

        var failedMessages = new List<string>();
        var deliveryTasks = new List<Task<DeliveryResult<Null, string>>>(validMessages.Count);

        var deliveryStopwatch = Stopwatch.StartNew();

        try
        {
            foreach (var message in validMessages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                deliveryTasks.Add(_producer.ProduceAsync(topic, new Message<Null, string> { Value = message }, cancellationToken));
            }

            DeliveryResult<Null, string>[] deliveryResults;
            try
            {
                deliveryResults = await Task.WhenAll(deliveryTasks).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "// KafkaProducer // ProduceAsync // Cancelled after scheduling {Count} messages. Topic={Topic}", deliveryTasks.Count, topic);

                throw;
            }

            int successDeliveriesCount = 0;
            for (int i = 0; i < deliveryResults.Length; i++)
            {
                if (deliveryResults[i].Status == PersistenceStatus.Persisted)
                {
                    successDeliveriesCount++;
                }
                else
                {
                    failedMessages.Add(validMessages[i]);
                    IncrementFailed();
                }
            }

            if (successDeliveriesCount > 0)
            {
                _publishedCounter.Add(successDeliveriesCount);
            }

            _logger.LogInformation(
                "// KafkaProducer // ProduceAsync // Topic={Topic} Total={Total} Success={Success} Failed={Failed} SuccessRate={Rate:P2} DurationMs={Duration:F0}",
                topic,
                validMessages.Count,
                successDeliveriesCount,
                failedMessages.Count,
                (double)successDeliveriesCount / validMessages.Count,
                deliveryStopwatch.Elapsed.TotalMilliseconds);

            return failedMessages;
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(
                ex,
                "// KafkaProducer // ProduceAsync // ProduceException Code={Code} Reason={Reason}",
                ex.Error.Code,
                ex.Error.Reason);

            IncrementFailed();
            if (ex.DeliveryResult?.Value is string failedMessage)
            {
                failedMessages.Add(failedMessage);
            }

            return failedMessages.Count > 0 ? failedMessages : validMessages;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Unexpected failure");

            IncrementFailed(validMessages.Count);

            return validMessages;
        }
        finally
        {
            if (deliveryStopwatch.IsRunning)
            {
                deliveryStopwatch.Stop();
                _batchLatencyMs.Record(deliveryStopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }

    /// <summary>
    /// Disposes the producer instance and attempts a graceful flush.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs cleanup of managed and unmanaged resources.
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
            _logger.LogError(ex, "// KafkaProducer // Dispose // Flush failed.");
        }
        finally
        {
            _producer.Dispose();
        }
    }

    /// <summary>
    /// Ensures that required topics exist by creating any missing topics through the Kafka admin client.
    /// </summary>
    private async Task EnsureTopicsExist()
    {
        using var adminClient = new AdminClientBuilder(AdminClientSettings).Build();

        Metadata metadata;
        try
        {
            metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // EnsureTopicsExist // Metadata fetch failed.");
            throw;
        }

        var existingTopics = new HashSet<string>(metadata.Topics.Select(e => e.Topic), StringComparer.OrdinalIgnoreCase);

        foreach (string topic in _settings.Admin.TopicList)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                _logger.LogWarning("// KafkaProducer // EnsureTopicsExist // Skipping empty topic.");

                continue;
            }

            if (existingTopics.Contains(topic))
            {
                continue;
            }

            try
            {
                await adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = topic,
                        Configs = TopicSpecification.Configs,
                        NumPartitions = TopicSpecification.NumPartitions,
                        ReplicationFactor = TopicSpecification.ReplicationFactor
                    }
                ]);

                _logger.LogInformation("// KafkaProducer // EnsureTopicsExist // Created topic '{Topic}'", topic);
            }
            catch (CreateTopicsException ex)
            {
                if (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    _logger.LogInformation("// KafkaProducer // EnsureTopicsExist // Topic '{Topic}' already exists.", topic);

                    continue;
                }

                _logger.LogError(ex, "// KafkaProducer // EnsureTopicsExist // Failed to create topic '{Topic}'", topic);
                throw;
            }
        }
    }

    /// <summary>
    /// Validates that the provided topic name is non-null and non-whitespace.
    /// </summary>
    private bool ValidateTopic(string topic)
    {
        if (!string.IsNullOrWhiteSpace(topic))
        {
            return true;
        }

        _logger.LogError("// KafkaProducer // Topic name is null, empty, or whitespace");

        IncrementFailed();

        return false;
    }

    /// <summary>
    /// Validates that the provided message content is non-null and non-whitespace.
    /// </summary>1
    private bool ValidateMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        _logger.LogError("// KafkaProducer // Message is null, empty, or whitespace");

        IncrementFailed();

        return false;
    }

    /// <summary>
    /// Builds and configures the producer settings used by the Kafka producer instance.
    /// </summary>
    private ProducerConfig BuildProducerConfig()
    {
        return new(ProducerSettings)
        {
            LingerMs = 20,
            Acks = Acks.All,
            MaxInFlight = 5,
            RetryBackoffMs = 1000,
            BatchSize = 256 * 1024,
            BatchNumMessages = 1000,
            EnableIdempotence = true,
            MessageSendMaxRetries = 5,
            EnableBackgroundPoll = true,
            SocketKeepaliveEnable = true,
            EnableDeliveryReports = true,
            StatisticsIntervalMs = 30000,
            DeliveryReportFields = "status",
            CompressionType = CompressionType.Zstd,
            ClientId = $"{_settings.Consumer.GroupId}-{GetType().Name.ToLower()}"
        };
    }

    /// <summary>
    /// Creates the Kafka producer instance and attaches error and statistics handlers.
    /// </summary>
    private IProducer<Null, string> BuildProducer(ProducerConfig config)
    {
        return new ProducerBuilder<Null, string>(config)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                {
                    _logger.LogCritical("// KafkaProducer // Fatal error {Code}: {Reason}", e.Code, e.Reason);
                }
                else if (e.IsError)
                {
                    _logger.LogError("// KafkaProducer // Broker error {Code}: {Reason}", e.Code, e.Reason);
                }
                else
                {
                    _logger.LogWarning("// KafkaProducer // Broker notice {Code}: {Reason}", e.Code, e.Reason);
                }
            })
            .SetStatisticsHandler((_, json) =>
            {
                _logger.LogDebug("// KafkaProducer // Stats: {StatsJson}", json);
            })
            .BuildWithInstrumentation();
    }

    /// <summary>
    /// Increments the failure counter, allowing batch operations to record multiple failures.
    /// </summary>
    private void IncrementFailed(int count = 1) => _failedCounter.Add(count);

    /// <summary>
    /// Increments the publish counter, allowing batch operations to record multiple successes.
    /// </summary>
    private void IncrementPublished(int count = 1) => _publishedCounter.Add(count);
}
