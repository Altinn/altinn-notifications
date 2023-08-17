using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka;

/// <summary>
/// Admin client responsible for preparing application for integration with Kafka
/// </summary>
public class AdminClient : BackgroundService
{
    private readonly SharedClientConfig _clientConfig;
    private readonly KafkaSettings _settings;
    private readonly ILogger<AdminClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminClient"/> class.
    /// </summary>
    public AdminClient(IOptions<KafkaSettings> settings, ILogger<AdminClient> logger)
    {
        _clientConfig = new SharedClientConfig(settings.Value);
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var adminClient = new AdminClientBuilder(_clientConfig.AdminClientConfig)
                    .Build();
        var existingTopics = adminClient.GetMetadata(TimeSpan.FromSeconds(10)).Topics;

        foreach (string topic in _settings.TopicList)
        {
            if (!existingTopics.Exists(t => t.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    adminClient.CreateTopicsAsync(new TopicSpecification[]
                    {
                        new TopicSpecification()
                        {
                            Name = topic,
                            NumPartitions = _clientConfig.TopicSpecification.NumPartitions,
                            ReplicationFactor = _clientConfig.TopicSpecification.ReplicationFactor
                        }
                    }).Wait();
                    _logger.LogInformation("// AdminClient // ExecuteAsync // Topic '{Topic}' created successfully.", topic);
                }
                catch (CreateTopicsException ex)
                {
                    _logger.LogError(ex, "// AdminClient // ExecuteAsync // Failed to create topic '{Topic}'", topic);
                    throw;
                }
            }
        }

        return Task.CompletedTask;
    }
}