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
    public async Task SendAsync_CustomTimeToLive_GatewayReferenceGenerated_SendingAccepted()
    {
        // Arrange
        var timeToLiveInSeconds = 5400;
        Guid notificationId = Guid.NewGuid();

        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>(), timeToLiveInSeconds))
            .ReturnsAsync("457418CB-FFDE-482C-BD53-1E8885CF87EF");

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.SmsStatusUpdatedTopicName))),
            It.Is<string>(s =>
            s.Contains("\"sendResult\":\"Accepted\"") &&
            s.Contains($"\"notificationId\":\"{notificationId}\"") &&
            s.Contains("\"gatewayReference\":\"457418CB-FFDE-482C-BD53-1E8885CF87EF\""))));

        var sendingService = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

        // Act
        await sendingService.SendAsync(sms, timeToLiveInSeconds);

        // Assert
        producerMock.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_DefaultTimeToLive_GatewayReferenceGenerated_SendingAccepted()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
            .ReturnsAsync("gateway-reference");

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.SmsStatusUpdatedTopicName))),
            It.Is<string>(s =>
            s.Contains("\"gatewayReference\":\"gateway-reference\"") &&
            s.Contains("\"sendResult\":\"Accepted\"") &&
            s.Contains($"\"notificationId\":\"{notificationId}\""))));

        var sendingService = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

        // Act
        await sendingService.SendAsync(sms);

        // Assert
        producerMock.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_CustomTimeToLive_InvalidRecipient_PublishedToExpectedKafkaTopic()
    {
        // Arrange
        var timeToLiveInSeconds = 12600;
        Guid notificationId = Guid.NewGuid();
        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>(), timeToLiveInSeconds))
        .ReturnsAsync(new SmsClientErrorResponse { SendResult = SmsSendResult.Failed_InvalidRecipient, ErrorMessage = "Receiver is invalid" });

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.SmsStatusUpdatedTopicName))),
            It.Is<string>(s =>
            s.Contains($"\"notificationId\":\"{notificationId}\"") &&
            s.Contains("\"sendResult\":\"Failed_InvalidRecipient\""))));

        var sendingService = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

        // Act
        await sendingService.SendAsync(sms, timeToLiveInSeconds);

        // Assert
        producerMock.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_DefaultTimeToLive_InvalidRecipient_PublishedToExpectedKafkaTopic()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
        .ReturnsAsync(new SmsClientErrorResponse { SendResult = SmsSendResult.Failed_InvalidRecipient, ErrorMessage = "Receiver is invalid" });

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.SmsStatusUpdatedTopicName))),
            It.Is<string>(s =>
            s.Contains($"\"notificationId\":\"{notificationId}\"") &&
            s.Contains("\"sendResult\":\"Failed_InvalidRecipient\""))));

        var sendingService = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

        // Act
        await sendingService.SendAsync(sms);

        // Assert
        producerMock.VerifyAll();
    }
}
