using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    private static IServiceProvider CreateServiceProvider(IMessageBus messageBus)
    {
        var innerSp = new Mock<IServiceProvider>();
        innerSp.Setup(sp => sp.GetService(typeof(IMessageBus))).Returns(messageBus);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(innerSp.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        return serviceProvider.Object;
    }

    [Fact]
    public async Task Handle_EmptyNotificationId_ThrowsArgumentException()
    {
        var command = ValidCommand(notificationId: Guid.Empty);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CheckEmailSendStatusHandler.Handle(
                Mock.Of<ILogger>(),
                Mock.Of<IDateTimeService>(),
                _topicSettings,
                Mock.Of<IServiceProvider>(),
                Mock.Of<ICommonProducer>(),
                Mock.Of<IEmailServiceClient>(),
                command));

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
                Mock.Of<ILogger>(),
                Mock.Of<IDateTimeService>(),
                _topicSettings,
                Mock.Of<IServiceProvider>(),
                Mock.Of<ICommonProducer>(),
                Mock.Of<IEmailServiceClient>(),
                command));

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
    public async Task Handle_TerminalSendResult_PublishesToKafkaAndDoesNotScheduleRetry(EmailSendResult terminalResult)
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

        var loggerMock = new Mock<ILogger>();
        var busMock = new Mock<IMessageBus>();

        // Act
        await CheckEmailSendStatusHandler.Handle(
            loggerMock.Object,
            Mock.Of<IDateTimeService>(),
            _topicSettings,
            CreateServiceProvider(busMock.Object),
            producerMock.Object,
            clientMock.Object,
            command);

        // Assert: terminal result goes to Kafka
        Assert.Equal(_topicSettings.EmailStatusUpdatedTopicName, capturedTopic);
        Assert.NotNull(capturedMessage);
        Assert.Contains(command.NotificationId.ToString(), capturedMessage);
        Assert.Contains(command.SendOperationId, capturedMessage);

        // Assert: no retry scheduled
        busMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_TerminalSendResult_LogsInformation()
    {
        // Arrange
        var command = ValidCommand();

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(EmailSendResult.Delivered);

        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);

        // Act
        await CheckEmailSendStatusHandler.Handle(
            loggerMock.Object,
            Mock.Of<IDateTimeService>(),
            _topicSettings,
            Mock.Of<IServiceProvider>(),
            producerMock.Object,
            clientMock.Object,
            command);

        // Assert: information log written for terminal status
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(command.NotificationId.ToString())),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SendResultIsSending_SchedulesRetryViaBusAndDoesNotPublishToKafka()
    {
        // Arrange
        var command = ValidCommand();
        var fixedTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(EmailSendResult.Sending);

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(fixedTime);

        CheckEmailSendStatusCommand? scheduledCommand = null;
        DeliveryOptions? scheduledOptions = null;

        var busMock = new Mock<IMessageBus>();
        busMock
            .Setup(b => b.PublishAsync(It.IsAny<CheckEmailSendStatusCommand>(), It.IsAny<DeliveryOptions>()))
            .Callback<CheckEmailSendStatusCommand, DeliveryOptions>((cmd, opts) =>
            {
                scheduledCommand = cmd;
                scheduledOptions = opts;
            })
            .Returns(ValueTask.CompletedTask);

        var producerMock = new Mock<ICommonProducer>();

        // Act
        await CheckEmailSendStatusHandler.Handle(
            Mock.Of<ILogger>(),
            dateTimeMock.Object,
            _topicSettings,
            CreateServiceProvider(busMock.Object),
            producerMock.Object,
            clientMock.Object,
            command);

        // Assert: retry scheduled with the 8-second delay
        busMock.Verify(b => b.PublishAsync(It.IsAny<CheckEmailSendStatusCommand>(), It.IsAny<DeliveryOptions>()), Times.Once);
        Assert.NotNull(scheduledCommand);
        Assert.Equal(command.NotificationId, scheduledCommand!.NotificationId);
        Assert.Equal(command.SendOperationId, scheduledCommand.SendOperationId);
        Assert.Equal(fixedTime, scheduledCommand.LastCheckedAtUtc);
        Assert.Equal(TimeSpan.FromMilliseconds(8000), scheduledOptions!.ScheduleDelay);

        // Assert: nothing published to Kafka
        producerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_SendResultIsSending_LogsDebug()
    {
        // Arrange
        var command = ValidCommand();

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(EmailSendResult.Sending);

        var busMock = new Mock<IMessageBus>();
        busMock
            .Setup(b => b.PublishAsync(It.IsAny<CheckEmailSendStatusCommand>(), It.IsAny<DeliveryOptions>()))
            .Returns(ValueTask.CompletedTask);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await CheckEmailSendStatusHandler.Handle(
            loggerMock.Object,
            Mock.Of<IDateTimeService>(),
            _topicSettings,
            CreateServiceProvider(busMock.Object),
            Mock.Of<ICommonProducer>(),
            clientMock.Object,
            command);

        // Assert: debug log written for still-sending
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(command.NotificationId.ToString())),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
