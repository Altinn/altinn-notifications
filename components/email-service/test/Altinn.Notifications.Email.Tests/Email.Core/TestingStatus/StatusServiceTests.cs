using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Status;

using Moq;
using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Core.Sending;

public class StatusServiceTests
{
    private readonly TopicSettings _topicSettings;

    public StatusServiceTests()
    {
        _topicSettings = new()
        {
            EmailStatusUpdatedTopicName = "EmailStatusUpdatedTopicName",
            EmailSendingAcceptedTopicName = "EmailSendingAcceptedTopicName"
        };
    }

    [Fact]
    public async Task UpdateSendStatus_OperationResultGenerated_PublishedToExpectedKafkaTopic()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SendNotificationOperationIdentifier identifier = new()
        {
            OperationId = "operation-id",
            NotificationId = id
        };

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.GetOperationUpdate(It.IsAny<string>()))
            .ReturnsAsync(EmailSendResult.Delivered);

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.EmailStatusUpdatedTopicName))),
            It.Is<string>(s =>
                s.Contains("\"operationId\":\"operation-id\"") &&
                s.Contains("\"sendResult\":\"Delivered\"") &&
                s.Contains($"\"notificationId\":\"{id}\""))));

        Mock<IDateTimeService> dateTimeMock = new();

        var sut = new StatusService(clientMock.Object, producerMock.Object, dateTimeMock.Object, _topicSettings);

        // Act
        await sut.UpdateSendStatus(identifier);

        // Assert
        producerMock.VerifyAll();
    }

    [Fact]
    public async Task UpdateSendStatus_SendResultIsSending_LatStatusCheckUpdatedAndPublishedBackOnTopic()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SendNotificationOperationIdentifier identifier = new()
        {
            OperationId = "operation-id",
            NotificationId = id,
            LastStatusCheck = new DateTime(1994, 06, 16, 08, 00, 00, DateTimeKind.Utc)
        };

        string actualProducerInput = string.Empty;

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.GetOperationUpdate(It.IsAny<string>()))
            .ReturnsAsync(EmailSendResult.Sending);

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.EmailSendingAcceptedTopicName))),
            It.IsAny<string>()))
              .Callback<string, string>((topicName, serializedIdentifier) =>
              {
                  actualProducerInput = serializedIdentifier;
              });

        Mock<IDateTimeService> dateTimeMock = new();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(new DateTime(2023, 06, 16, 08, 00, 00, DateTimeKind.Utc));

        var sut = new StatusService(clientMock.Object, producerMock.Object, dateTimeMock.Object, _topicSettings);

        // Act
        await sut.UpdateSendStatus(identifier);

        // Assert
        producerMock.VerifyAll();
        Assert.Contains("\"operationId\":\"operation-id\"", actualProducerInput);
        Assert.Contains($"\"notificationId\":\"{id}\"", actualProducerInput);
        Assert.Contains("\"lastStatusCheck\":\"2023-06-16T08:00:00Z\"", actualProducerInput);
    }

    [Fact]
    public async Task UpdateSendStatus_PublishedToExpectedKafkaTopic()
    {
        SendOperationResult result = new()
        {
            OperationId = "00000000-0000-0000-0000-000000000000",
            SendResult = EmailSendResult.Delivered
        };

        Mock<IEmailServiceClient> clientMock = new();

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(
            It.Is<string>(s => s.Equals(nameof(_topicSettings.EmailStatusUpdatedTopicName))),
            It.Is<string>(s =>
                s.Contains("\"operationId\":\"00000000-0000-0000-0000-000000000000\"") &&
                s.Contains("\"sendResult\":\"Delivered\""))));
        
        Mock<IDateTimeService> dateTimeMock = new();

        StatusService statusService = new(clientMock.Object, producerMock.Object, dateTimeMock.Object, _topicSettings);

        await statusService.UpdateSendStatus(result);

        producerMock.VerifyAll();
        Assert.NotNull(result);
    }
}
