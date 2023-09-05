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
            EmailSendingAcceptedRetryTopicName = "EmailSendingAcceptedRetryTopicName"
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

        var sut = new StatusService(clientMock.Object, producerMock.Object, _topicSettings);

        // Act
        await sut.UpdateSendStatus(identifier);

        // Assert
        producerMock.VerifyAll();
    }
}
