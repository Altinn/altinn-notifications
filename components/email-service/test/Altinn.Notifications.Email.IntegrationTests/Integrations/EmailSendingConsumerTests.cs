using System.Text.Json;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Producers;
using Altinn.Notifications.Email.IntegrationTests.Utils;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTests.Integrations;

public sealed class EmailSendingConsumerTests : IAsyncLifetime
{
    private readonly string EmailSendingConsumerTopic = Guid.NewGuid().ToString();
    private readonly string EmailSendingAcceptedProducerTopic = Guid.NewGuid().ToString();

    private readonly KafkaSettings _kafkaSettings;
    private ServiceProvider? _serviceProvider;

    public EmailSendingConsumerTests()
    {
        _kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            Consumer = new()
            {
                GroupId = "email-sending-consumer"
            },
            SendEmailQueueTopicName = EmailSendingConsumerTopic,
            EmailSendingAcceptedTopicName = EmailSendingAcceptedProducerTopic,
            Admin = new()
            {
                TopicList = new List<string> { EmailSendingConsumerTopic, EmailSendingAcceptedProducerTopic }
            }
        };
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await KafkaUtil.DeleteTopicAsync(EmailSendingConsumerTopic);
        await KafkaUtil.DeleteTopicAsync(EmailSendingAcceptedProducerTopic);
    }

    [Fact]
    public async Task ConsumeEmailTest_Successfull_deserialization_of_message_Service_called_once()
    {
        // Arrange
        Mock<ISendingService> serviceMock = new();
        serviceMock.Setup(es => es.SendAsync(It.IsAny<Core.Sending.Email>()));

        Core.Sending.Email email =
            new(Guid.NewGuid(), "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

        using SendEmailQueueConsumer sut = GetEmailSendingConsumer(serviceMock.Object);
        using CommonProducer kafkaProducer = KafkaUtil.GetKafkaProducer(_serviceProvider!);

        // Act
        await kafkaProducer.ProduceAsync(EmailSendingConsumerTopic, JsonSerializer.Serialize(email));

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        serviceMock.Verify(es => es.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeEmailTest_Deserialization_of_message_fails_Never_calls_service()
    {
        // Arrange
        Mock<ISendingService> serviceMock = new();
        serviceMock.Setup(es => es.SendAsync(It.IsAny<Core.Sending.Email>()));
        using SendEmailQueueConsumer sut = GetEmailSendingConsumer(serviceMock.Object);
        using CommonProducer kafkaProducer = KafkaUtil.GetKafkaProducer(_serviceProvider!);

        // Act
        await kafkaProducer.ProduceAsync(EmailSendingConsumerTopic, "Not an email");

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        serviceMock.Verify(es => es.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Never);
    }

    private SendEmailQueueConsumer GetEmailSendingConsumer(ISendingService sendingService)
    {

        IServiceCollection services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_kafkaSettings)
            .AddSingleton<ICommonProducer, CommonProducer>()
            .AddSingleton(sendingService)
            .AddHostedService<SendEmailQueueConsumer>();

        _serviceProvider = services.BuildServiceProvider();

        var emailSendingConsumer = _serviceProvider.GetService(typeof(IHostedService)) as SendEmailQueueConsumer;

        if (emailSendingConsumer == null)
        {
            Assert.Fail("Unable to create an instance of EmailSendingConsumer.");
        }

        return emailSendingConsumer;
    }
}
