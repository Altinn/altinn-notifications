using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Kafka.Publishers;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Kafka;

public class KafkaEmailCommandPublisherTests
{
    private const string _topicName = "altinn.notifications.email.queue";

    private readonly Email _email = new(Guid.NewGuid(), "subject", "body", "from@domain.com", "to@domain.com", EmailContentType.Plain);

    [Fact]
    public async Task PublishAsync_Single_ProducerSucceeds_ReturnsNull()
    {
        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<string>())).ReturnsAsync(true);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        var result = await publisher.PublishAsync(_email, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_Single_ProducerFails_ReturnsEmail()
    {
        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<string>())).ReturnsAsync(false);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        var result = await publisher.PublishAsync(_email, CancellationToken.None);

        Assert.Equal(_email, result);
    }

    [Fact]
    public async Task PublishAsync_Single_PreCancelledToken_ThrowsWithoutCallingProducer()
    {
        var producer = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, cts.Token));

        producer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Batch_EmptyList_ReturnsEmptyWithoutCallingProducer()
    {
        var producer = new Mock<IKafkaProducer>();
        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        var result = await publisher.PublishAsync([], CancellationToken.None);

        Assert.Empty(result);
        producer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Batch_AllPublishedSuccessfully_ReturnsEmpty()
    {
        var email1 = new Email(Guid.NewGuid(), "s1", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "s2", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email3 = new Email(Guid.NewGuid(), "s3", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var batch = new List<Email> { email1, email2, email3 };

        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.Is<ImmutableList<string>>(m => m.Count == batch.Count), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList<string>.Empty);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        var result = await publisher.PublishAsync(batch, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PublishAsync_Batch_AllFailed_ReturnsAllEmails()
    {
        var email1 = new Email(Guid.NewGuid(), "s1", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "s2", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email3 = new Email(Guid.NewGuid(), "s3", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var batch = new List<Email> { email1, email2, email3 };

        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([email1.Serialize(), email2.Serialize(), email3.Serialize()]);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        var result = await publisher.PublishAsync(batch, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, e => e.NotificationId == email1.NotificationId);
        Assert.Contains(result, e => e.NotificationId == email2.NotificationId);
        Assert.Contains(result, e => e.NotificationId == email3.NotificationId);
    }

    [Fact]
    public async Task PublishAsync_Batch_PartialFailure_ReturnsOnlyFailedEmails()
    {
        var email1 = new Email(Guid.NewGuid(), "s1", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "s2", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email3 = new Email(Guid.NewGuid(), "s3", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var batch = new List<Email> { email1, email2, email3 };

        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([email2.Serialize()]);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        var result = await publisher.PublishAsync(batch, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(email2.NotificationId, result[0].NotificationId);
    }

    [Fact]
    public async Task PublishAsync_Batch_EntriesWithEmptyNotificationId_AreSkipped()
    {
        // "{}" is valid JSON. It deserializes to an Email with Guid.Empty NotificationId and is skipped.
        var email1 = new Email(Guid.NewGuid(), "s1", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "s2", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var batch = new List<Email> { email1, email2 };

        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([email1.Serialize(), "{}", email2.Serialize()]);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        var result = await publisher.PublishAsync(batch, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.NotificationId == email1.NotificationId);
        Assert.Contains(result, e => e.NotificationId == email2.NotificationId);
    }

    [Fact]
    public async Task PublishAsync_Batch_MalformedSerializedEntry_ThrowsJsonException()
    {
        var email1 = new Email(Guid.NewGuid(), "s1", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var batch = new List<Email> { email1 };

        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([email1.Serialize(), "{"]);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        await Assert.ThrowsAsync<JsonException>(() => publisher.PublishAsync(batch, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_Batch_ProducerThrowsOperationCanceled_Propagates()
    {
        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync([_email], CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_Batch_SerializesAllEmailsToProducer()
    {
        var email1 = new Email(Guid.NewGuid(), "s1", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "s2", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var batch = new List<Email> { email1, email2 };

        ImmutableList<string>? capturedMessages = null;
        var producer = new Mock<IKafkaProducer>();
        producer.Setup(p => p.ProduceAsync(_topicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, ImmutableList<string>, CancellationToken>((_, msgs, _) => capturedMessages = msgs)
            .ReturnsAsync(ImmutableList<string>.Empty);

        var publisher = new KafkaEmailCommandPublisher(producer.Object, _topicName);

        await publisher.PublishAsync(batch, CancellationToken.None);

        Assert.NotNull(capturedMessages);
        Assert.Equal(2, capturedMessages!.Count);
        Assert.Equal(email1.Serialize(), capturedMessages[0]);
        Assert.Equal(email2.Serialize(), capturedMessages[1]);
    }
}
