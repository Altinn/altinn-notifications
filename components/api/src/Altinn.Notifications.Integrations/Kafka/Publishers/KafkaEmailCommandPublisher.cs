using System.Collections.Immutable;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Publishers;

/// <summary>
/// Kafka-based implementation of <see cref="IEmailCommandPublisher"/> that publishes
/// email notifications to a Kafka topic via <see cref="IKafkaProducer"/>.
/// </summary>
internal sealed class KafkaEmailCommandPublisher(IKafkaProducer kafkaProducer, IOptions<KafkaSettings> options) : IEmailCommandPublisher
{
    private readonly IKafkaProducer _kafkaProducer = kafkaProducer;
    private readonly string _topicName = options.Value.EmailQueueTopicName;

    /// <inheritdoc/>
    public async Task<Email?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool success = await _kafkaProducer.ProduceAsync(_topicName, email.Serialize());
        return success ? null : email;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Email>> PublishAsync(IReadOnlyList<Email> emails, CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        var serializedEmails = emails.Select(e => e.Serialize()).ToImmutableList();
        var unpublishedSerialized = await _kafkaProducer.ProduceAsync(_topicName, serializedEmails, cancellationToken);

        var failedEmails = new List<Email>();
        foreach (var unpublished in unpublishedSerialized)
        {
            var deserialized = JsonSerializer.Deserialize<Email>(unpublished, JsonSerializerOptionsProvider.Options);
            if (deserialized is not null && deserialized.NotificationId != Guid.Empty)
            {
                failedEmails.Add(deserialized);
            }
        }

        return failedEmails;
    }
}
