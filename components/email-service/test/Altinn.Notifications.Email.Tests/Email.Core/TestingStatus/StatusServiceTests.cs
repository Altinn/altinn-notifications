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

    [Theory]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Delivered)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_Quarantined)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    [InlineData(EmailSendResult.Failed_TransientError)]
    [InlineData(EmailSendResult.Failed_InvalidEmailFormat)]
    [InlineData(EmailSendResult.Failed_SupressedRecipient)]
    public async Task UpdateSendStatus_TerminalResult_DispatchesWithCorrectFieldsAndDoesNotProduce(EmailSendResult terminalResult)
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SendNotificationOperationIdentifier identifier = new()
        {
            OperationId = "operation-id",
            NotificationId = id
        };

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.GetOperationUpdate(identifier.OperationId))
            .ReturnsAsync(terminalResult);

        Mock<ICommonProducer> producerMock = new();
        Mock<IEmailSendResultDispatcher> dispatcherMock = new();
        dispatcherMock.Setup(d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
            r.OperationId == identifier.OperationId &&
            r.NotificationId == id &&
            r.SendResult == terminalResult)))
            .Returns(Task.CompletedTask);

        Mock<IDateTimeService> dateTimeMock = new();

        var statusService = new StatusService(_topicSettings, producerMock.Object, dateTimeMock.Object, clientMock.Object, dispatcherMock.Object);

        // Act
        await statusService.UpdateSendStatus(identifier);

        // Assert
        dispatcherMock.VerifyAll();
        producerMock.VerifyNoOtherCalls();
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
              })
              .ReturnsAsync(true);

        Mock<IEmailSendResultDispatcher> dispatcherMock = new();
        Mock<IDateTimeService> dateTimeMock = new();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(new DateTime(2023, 06, 16, 08, 00, 00, DateTimeKind.Utc));

        var statusService = new StatusService(_topicSettings, producerMock.Object, dateTimeMock.Object, clientMock.Object, dispatcherMock.Object);

        // Act
        await statusService.UpdateSendStatus(identifier);

        // Assert
        producerMock.VerifyAll();
        Assert.Contains($"\"notificationId\":\"{id}\"", actualProducerInput);
        Assert.Contains("\"operationId\":\"operation-id\"", actualProducerInput);
        Assert.Contains("\"lastStatusCheck\":\"2023-06-16T08:00:00Z\"", actualProducerInput);
        dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<SendOperationResult>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_SendResultIsSending_WhenProduceFails_ThrowsInvalidOperationException()
    {
        // Arrange
        SendNotificationOperationIdentifier identifier = new()
        {
            OperationId = "operation-id",
            NotificationId = Guid.NewGuid()
        };

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.GetOperationUpdate(It.IsAny<string>()))
            .ReturnsAsync(EmailSendResult.Sending);

        Mock<ICommonProducer> producerMock = new();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        Mock<IEmailSendResultDispatcher> dispatcherMock = new();
        Mock<IDateTimeService> dateTimeMock = new();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(DateTime.UtcNow);

        var statusService = new StatusService(_topicSettings, producerMock.Object, dateTimeMock.Object, clientMock.Object, dispatcherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => statusService.UpdateSendStatus(identifier));
        dispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateSendStatus_WithSendOperationResult_DispatchesToStatusDispatcher()
    {
        // Arrange
        SendOperationResult result = new()
        {
            SendResult = EmailSendResult.Delivered,
            OperationId = "00000000-0000-0000-0000-000000000000"
        };

        Mock<ICommonProducer> producerMock = new();
        Mock<IEmailServiceClient> clientMock = new();
        Mock<IEmailSendResultDispatcher> dispatcherMock = new();

        dispatcherMock.Setup(d => d.DispatchAsync(result)).Returns(Task.CompletedTask);

        Mock<IDateTimeService> dateTimeMock = new();

        StatusService statusService = new(_topicSettings, producerMock.Object, dateTimeMock.Object, clientMock.Object, dispatcherMock.Object);

        // Act
        await statusService.UpdateSendStatus(result);

        // Assert
        dispatcherMock.VerifyAll();
        clientMock.VerifyNoOtherCalls();
        producerMock.VerifyNoOtherCalls();
    }
}
