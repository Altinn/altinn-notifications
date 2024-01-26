using Altinn.Notifications.Sms.Core.Configuration;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Status;
using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Core.Sending;

public class SendingServiceTests
{
    private readonly TopicSettings _topicSettings;

    public SendingServiceTests()
    {
        _topicSettings = new()
        {
            SmsStatusUpdatedTopicName = "SmsStatusUpdatedTopicName"
        };
    }

    [Fact]
    public async Task SendAsync_GatewayReferenceGenerated_SendingAccepted()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Sms.Core.Sending.Sms sms = new(id, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
            .ReturnsAsync("gateway-reference");

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.SmsStatusUpdatedTopicName))),
            It.Is<string>(s =>
            s.Contains("\"gatewayReference\":\"gateway-reference\"") &&
            s.Contains("\"sendResult\":\"Accepted\"") &&
            s.Contains($"\"notificationId\":\"{id}\""))));

        var sut = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

        // Act
        await sut.SendAsync(sms);

        // Assert
        producerMock.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_InvalidReceiver_PublishedToExpectedKafkaTopic()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Sms.Core.Sending.Sms sms = new(id, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
        .ReturnsAsync(new SmsClientErrorResponse { SendResult = SmsSendResult.Failed_InvalidReceiver, ErrorMessage = "Receiver is wrong" });

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.SmsStatusUpdatedTopicName))),
            It.Is<string>(s =>
            s.Contains($"\"notificationId\":\"{id}\"") &&
            s.Contains("\"sendResult\":\"Failed_InvalidReceiver\""))));

        var sut = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

        // Act
        await sut.SendAsync(sms);

        // Assert
        producerMock.VerifyAll();
    }
}
