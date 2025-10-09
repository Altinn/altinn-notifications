using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
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

public class NotificationStatusConsumerBaseTests : IAsyncLifetime
{
    private const string _emailTopic = "altinn.notifications.email.queue";
    private const string _smsStatusTopic = "altinn.notifications.sms.status.updated";
    private const string _emailStatusTopic = "altinn.notifications.email.status.updated";
    private const string _emailStatusRetryTopic = "altinn.notifications.email.status.updated.retry";

    /// <summary>
    /// Called immediately after the class has been created, before it is used.
    /// </summary>
    /// <returns></returns>
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when an object is no longer needed.
    /// </summary>
    public async Task DisposeAsync()
    {
        await Dispose(true);
    }

    [Fact]
    public async Task ProcessEmailDeliveryReport_WhenUnexpectedException_RetryDirectedToRetryTopic()
    {
        // Arrange
        var kafkaSettings = BuildKafkaSettings();
        var guidService = new Mock<IGuidService>();
        var dateTimeService = new Mock<IDateTimeService>();
        var producer = new Mock<IKafkaProducer>(MockBehavior.Loose);
        var emailNotificationRepository = new Mock<IEmailNotificationRepository>();

        var emailSendOperationResult = new EmailSendOperationResult
        {
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var deliveryReportMessage = emailSendOperationResult.Serialize();

        emailNotificationRepository
            .Setup(e => e.UpdateSendStatus(null, EmailNotificationResultType.Delivered, emailSendOperationResult.OperationId))
            .ThrowsAsync(new InvalidOperationException());

        producer
            .Setup(e => e.ProduceAsync(kafkaSettings.Value.EmailStatusUpdatedTopicName, It.Is<string>(m => m.Equals(deliveryReportMessage))))
            .ReturnsAsync(true);

        var emailNotificationService = new EmailNotificationService(
            guidService.Object,
            producer.Object,
            dateTimeService.Object,
            Options.Create(new Altinn.Notifications.Core.Configuration.KafkaSettings
            {
                EmailQueueTopicName = kafkaSettings.Value.EmailQueueTopicName
            }),
            emailNotificationRepository.Object);

        using var emailStatusConsumer = new EmailStatusConsumer(producer.Object, kafkaSettings, NullLogger<EmailStatusConsumer>.Instance, emailNotificationService);

        // Act
        await emailStatusConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        await KafkaUtil.PublishMessageOnTopic(kafkaSettings.Value.EmailStatusUpdatedTopicName, deliveryReportMessage);

        // Assert
        await IntegrationTestUtil.EventuallyAsync(
            () => producer.Invocations.Any(i => i.Method.Name == nameof(IKafkaProducer.ProduceAsync) &&
                                                i.Arguments[0] is string topic && topic == kafkaSettings.Value.EmailStatusUpdatedTopicName &&
                                                i.Arguments[1] is string message && message == deliveryReportMessage),
            TimeSpan.FromSeconds(15));

        await emailStatusConsumer.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual async Task Dispose(bool disposing)
    {
        await KafkaUtil.DeleteTopicAsync(_emailTopic);
        await KafkaUtil.DeleteTopicAsync(_smsStatusTopic);
        await KafkaUtil.DeleteTopicAsync(_emailStatusTopic);
        await KafkaUtil.DeleteTopicAsync(_emailStatusRetryTopic);
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
    private static IOptions<KafkaSettings> BuildKafkaSettings()
    {
        return Options.Create(new KafkaSettings
        {
            Admin = new AdminSettings(),
            BrokerAddress = "localhost:9092",
            Producer = new ProducerSettings(),
            EmailQueueTopicName = _emailTopic,
            SmsStatusUpdatedTopicName = _smsStatusTopic,
            EmailStatusUpdatedTopicName = _emailStatusTopic,
            Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
        });
    }
}
