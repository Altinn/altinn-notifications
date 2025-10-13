using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class NotificationStatusRetryConsumerBaseTests : IAsyncLifetime
{
    private readonly string _emailStatusUpdatedRetryTopicName = Guid.NewGuid().ToString();

    [Fact]
    public async Task RetryMessageBeyondThresholdValue_WhenUnexpectedException_RetryDirectedToRetryTopic()
    {
        // Arrange
        var kafkaSettings = BuildKafkaSettings();
        var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var deadDeliveryReportRepositoryMock = new Mock<IDeadDeliveryReportRepository>();
        var emailNotificationServiceMock = new Mock<IEmailNotificationService>();

        var emailSendOperationResultSerialized = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        }.Serialize();

        var retryMessage = new UpdateStatusRetryMessage
        {
            Attempts = 1,
            FirstSeen = DateTime.UtcNow.AddMinutes(-10), // should hit threshold
            LastAttempt = DateTime.UtcNow,
            SendOperationResult = emailSendOperationResultSerialized
        };

        deadDeliveryReportRepositoryMock
            .Setup(e => e.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        producer
            .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, It.Is<string>(m => m.Equals(retryMessage.Serialize()))))
            .ReturnsAsync(true);

        var deadDeliveryReportService = new DeadDeliveryReportService(deadDeliveryReportRepositoryMock.Object);

        using var emailStatusConsumer = new EmailStatusRetryConsumer(producer.Object, emailNotificationServiceMock.Object, deadDeliveryReportService, kafkaSettings, NullLogger<EmailStatusRetryConsumer>.Instance);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedRetryTopicName, retryMessage.Serialize());

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () => producer.Invocations.Any(i => i.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                                                i.Arguments[0] is string topic && topic == kafkaSettings.Value.EmailStatusUpdatedRetryTopicName &&
                                                i.Arguments[1] is string message && !string.IsNullOrWhiteSpace(message) && JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options)?.SendOperationResult == retryMessage.SendOperationResult),
            TimeSpan.FromSeconds(10));

        await emailStatusConsumer.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Creates Kafka settings.
    /// </summary>
    /// <returns>
    /// An <see cref="IOptions{KafkaSettings}"/> instance with minimal configuration needed for running the notification consumer tests.
    /// </returns>
    /// <remarks>
    /// Provides a standard configuration with localhost broker address and unit-tests group ID.
    /// </remarks>
    private IOptions<KafkaSettings> BuildKafkaSettings()
    {
        return Options.Create(new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            StatusUpdatedRetryThresholdSeconds = 300, // 5 minutes
            EmailStatusUpdatedRetryTopicName = _emailStatusUpdatedRetryTopicName,
            Producer = new ProducerSettings(),
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });
    }

    public async Task DisposeAsync()
    {
        await KafkaUtil.DeleteTopicAsync(_emailStatusUpdatedRetryTopicName);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
