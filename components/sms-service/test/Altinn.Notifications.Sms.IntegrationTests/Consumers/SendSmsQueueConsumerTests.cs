using System.Text.Json;

using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Consumers;
using Altinn.Notifications.Sms.Integrations.Producers;
using Altinn.Notifications.Sms.IntegrationTests.Utils;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

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
            SendSmsQueueRetryTopicName = _sendSmsQueueRetryTopicName,
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
    public async Task ConsumeSms_ValidMessage_SendingServiceInvoked()
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

        bool sendingServiceCalled = false;
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    sendingServiceMock.Verify(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()), Times.Once);
                    sendingServiceCalled = true;
                    return sendingServiceCalled;
                }
                catch (Exception)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(sendingServiceCalled);
    }

    [Fact]
    public async Task ConsumeSms_InvalidSms_SendingServiceNeverCalled()
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>())).Returns(Task.CompletedTask);

        using SendSmsQueueConsumer queueConsumer = GetSmsSendingConsumer(sendingServiceMock.Object);
        using CommonProducer commonProducer = KafkaUtil.GetKafkaProducer(_serviceProvider!);

        // Act
        await commonProducer.ProduceAsync(_sendSmsQueueTopicName, JsonSerializer.Serialize("Crap sms"));

        await queueConsumer.StartAsync(CancellationToken.None);

        bool sendingServiceNeverInvoked = false;
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    sendingServiceMock.Verify(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()), Times.Never);
                    sendingServiceNeverInvoked = true;
                    return sendingServiceNeverInvoked;
                }
                catch (Exception)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(sendingServiceNeverInvoked);
    }

    [Fact]
    public async Task ConsumeSms_SendingServiceThrowsException_MessagePutOnRetryTopic()
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        var producerMock = new Mock<ICommonProducer>();
        var sendMessageException = new Exception("504 Gateway Timeout");
        sendingServiceMock.Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()))
            .ThrowsAsync(sendMessageException);
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        Core.Sending.Sms sms = new(Guid.NewGuid(), "sender", "recipient", "message");
        string smsJson = JsonSerializer.Serialize(sms);

        using SendSmsQueueConsumer queueConsumer = GetSmsSendingConsumer(sendingServiceMock.Object, producerMock.Object);
        using CommonProducer commonProducer = KafkaUtil.GetKafkaProducer(_serviceProvider!);

        // Act
        await commonProducer.ProduceAsync(_sendSmsQueueTopicName, smsJson);

        await queueConsumer.StartAsync(CancellationToken.None);

        bool sendingServiceCalledOnce = false;
        bool messagePublishedToRetryTopic = false;
        await IntegrationTestUtil.EventuallyAsync(
            () =>
            {
                try
                {
                    sendingServiceMock.Verify(s => s.SendAsync(It.IsAny<Core.Sending.Sms>()), Times.Once);
                    sendingServiceCalledOnce = true;

                    producerMock.Verify(p => p.ProduceAsync(_sendSmsQueueRetryTopicName, smsJson), Times.Once);
                    messagePublishedToRetryTopic = true;

                    return sendingServiceCalledOnce && messagePublishedToRetryTopic;
                }
                catch (Exception)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(100));

        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(sendingServiceCalledOnce);
        Assert.True(messagePublishedToRetryTopic);
    }

    private SendSmsQueueConsumer GetSmsSendingConsumer(ISendingService sendingService, ICommonProducer? producer = null)
    {
        // Always initialize service provider for KafkaUtil.GetKafkaProducer
        IServiceCollection services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_kafkaSettings)
            .AddSingleton<ICommonProducer, CommonProducer>()
            .AddSingleton(sendingService)
            .AddHostedService<SendSmsQueueConsumer>();

        _serviceProvider = services.BuildServiceProvider();

        if (producer == null)
        {
            var smsSendingConsumer = _serviceProvider.GetService(typeof(IHostedService)) as SendSmsQueueConsumer;

            if (smsSendingConsumer == null)
            {
                Assert.Fail("Unable to create an instance of SmsSendingConsumer.");
            }

            return smsSendingConsumer;
        }
        else
        {
            return new SendSmsQueueConsumer(
                _kafkaSettings,
                sendingService,
                producer,
                NullLogger<SendSmsQueueConsumer>.Instance);
        }
    }
}
