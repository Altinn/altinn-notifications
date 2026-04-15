using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations.Wolverine;

public class CheckEmailSendStatusHandlerTests
{
    private static readonly TopicSettings _topicSettings = new()
    {
        EmailStatusUpdatedTopicName = "email.status.updated"
    };

    private static CheckEmailSendStatusCommand ValidCommand(Guid? notificationId = null, string operationId = "op-123") =>
        new()
        {
            SendOperationId = operationId,
            LastCheckedAtUtc = DateTime.UtcNow,
            NotificationId = notificationId ?? Guid.NewGuid()
        };

    [Fact]
    public async Task Handle_EmptyNotificationId_ThrowsArgumentException()
    {
        var command = ValidCommand(notificationId: Guid.Empty);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CheckEmailSendStatusHandler.Handle(
                command,
                Mock.Of<IDateTimeService>(),
                _topicSettings,
                Mock.Of<IMessageContext>(),
                Mock.Of<ICommonProducer>(),
                Mock.Of<IEmailServiceClient>()));

        Assert.Equal("checkEmailSendStatusCommand", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceSendOperationId_ThrowsArgumentException(string operationId)
    {
        var command = ValidCommand(operationId: operationId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CheckEmailSendStatusHandler.Handle(
                command,
                Mock.Of<IDateTimeService>(),
                _topicSettings,
                Mock.Of<IMessageContext>(),
                Mock.Of<ICommonProducer>(),
                Mock.Of<IEmailServiceClient>()));

        Assert.Equal("checkEmailSendStatusCommand", ex.ParamName);
    }

    [Theory]
    [InlineData(EmailSendResult.Delivered)]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Failed_InvalidEmailFormat)]
    [InlineData(EmailSendResult.Failed_SupressedRecipient)]
    [InlineData(EmailSendResult.Failed_TransientError)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    [InlineData(EmailSendResult.Failed_Quarantined)]
    public async Task Handle_TerminalSendResult_PublishesToKafka(EmailSendResult terminalResult)
    {
        // Arrange
        var command = ValidCommand();

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(terminalResult);

        string? capturedTopic = null;
        string? capturedMessage = null;

        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((topic, msg) =>
            {
                capturedTopic = topic;
                capturedMessage = msg;
            })
            .ReturnsAsync(true);

        // Act
        await CheckEmailSendStatusHandler.Handle(
            command,
            Mock.Of<IDateTimeService>(),
            _topicSettings,
            Mock.Of<IMessageContext>(),
            producerMock.Object,
            clientMock.Object);

        // Assert: terminal result goes to Kafka
        Assert.Equal(_topicSettings.EmailStatusUpdatedTopicName, capturedTopic);
        Assert.NotNull(capturedMessage);
        Assert.Contains(command.NotificationId.ToString(), capturedMessage);
        Assert.Contains(command.SendOperationId, capturedMessage);
    }

    [Fact]
    public async Task Handle_SendResultIsSending_SchedulesRecheckAndDoesNotPublishToKafka()
    {
        // Arrange
        var command = ValidCommand();
        var fixedTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(EmailSendResult.Sending);

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(fixedTime);

        var producerMock = new Mock<ICommonProducer>();
        var messageContextMock = new Mock<IMessageContext>();

        // Act
        await CheckEmailSendStatusHandler.Handle(
            command,
            dateTimeMock.Object,
            _topicSettings,
            messageContextMock.Object,
            producerMock.Object,
            clientMock.Object);

        // Assert
        // ScheduleAsync is a Wolverine extension method that resolves to PublishAsync with DeliveryOptions.ScheduleDelay,
        // so we verify PublishAsync since that is the interface method Moq can intercept.
        messageContextMock.Verify(
            m => m.PublishAsync(
                It.Is<CheckEmailSendStatusCommand>(c =>
                    c.NotificationId == command.NotificationId &&
                    c.SendOperationId == command.SendOperationId &&
                    c.LastCheckedAtUtc == fixedTime),
                It.Is<DeliveryOptions?>(o => o != null && o.ScheduleDelay == TimeSpan.FromMilliseconds(8000))),
            Times.Once);

        producerMock.VerifyNoOtherCalls();
    }
}
