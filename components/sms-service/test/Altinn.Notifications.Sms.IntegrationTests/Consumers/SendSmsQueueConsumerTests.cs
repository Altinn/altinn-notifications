using System.Text.Json;

using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Consumers;
using Altinn.Notifications.Sms.Integrations.Producers;
using Altinn.Notifications.Sms.IntegrationTests.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

namespace Altinn.Notifications.Sms.IntegrationTests.Consumers;

public class SendSmsQueueConsumerTests : IAsyncLifetime
{
    private readonly string _sendSmsQueueTopicName = Guid.NewGuid().ToString();
    private readonly string _sendSmsQueueRetryTopicName = Guid.NewGuid().ToString();
    private readonly KafkaSettings _kafkaSettings;
    private ServiceProvider? _serviceProvider;

    public SendSmsQueueConsumerTests()
    {
        _kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            Consumer = new()
            {
                GroupId = "sms-sending-consumer"
            },
            SendSmsQueueTopicName = _sendSmsQueueTopicName,
            Admin = new()
            {
                TopicList = new List<string> { _sendSmsQueueTopicName, _sendSmsQueueRetryTopicName }
            }
        };
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await KafkaUtil.DeleteTopicAsync(_sendSmsQueueTopicName);
        await KafkaUtil.DeleteTopicAsync(_sendSmsQueueRetryTopicName);
    }

    [Fact]
    public async Task ConsumeSms_SendingServiceCalledOnce_Success()
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>())).Returns(Task.CompletedTask);

        Core.Sending.Sms sms = new(Guid.NewGuid(), "sender", "recipient", "message");

        using SendSmsQueueConsumer queueConsumer = GetSmsSendingConsumer(sendingServiceMock.Object);
        using CommonProducer commonProducer = KafkaUtil.GetKafkaProducer(_serviceProvider!);

        // Act
        await commonProducer.ProduceAsync(_sendSmsQueueTopicName, JsonSerializer.Serialize(sms));

        await queueConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        sendingServiceMock.Verify(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeSms_InvalidSms_SendingServiceNeverCalled_Fail()
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>())).Returns(Task.CompletedTask);

        using SendSmsQueueConsumer queueConsumer = GetSmsSendingConsumer(sendingServiceMock.Object);
        using CommonProducer commonProducer = KafkaUtil.GetKafkaProducer(_serviceProvider!);

        // Act
        await commonProducer.ProduceAsync(_sendSmsQueueTopicName, JsonSerializer.Serialize("Crap sms"));

        await queueConsumer.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        sendingServiceMock.Verify(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()), Times.Never);
    }

    private SendSmsQueueConsumer GetSmsSendingConsumer(ISendingService sendingService)
    {
        IServiceCollection services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_kafkaSettings)
            .AddSingleton<ICommonProducer, CommonProducer>()
            .AddSingleton(sendingService)
            .AddHostedService<SendSmsQueueConsumer>();

        _serviceProvider = services.BuildServiceProvider();

        var smsSendingConsumer = _serviceProvider.GetService(typeof(IHostedService)) as SendSmsQueueConsumer;

        if (smsSendingConsumer == null)
        {
            Assert.Fail("Unable to create an instance of SmsSendingConsumer.");
        }

        return smsSendingConsumer;
    }
}
