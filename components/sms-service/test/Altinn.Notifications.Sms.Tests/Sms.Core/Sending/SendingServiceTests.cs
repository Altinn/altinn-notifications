using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Status;

using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Core.Sending;

public class SendingServiceTests
{
    [Fact]
    public async Task SendAsync_CustomTimeToLive_GatewayReferenceGenerated_SendingAccepted()
    {
        // Arrange
        var timeToLiveInSeconds = 5400;
        Guid notificationId = Guid.NewGuid();
        const string gatewayReference = "457418CB-FFDE-482C-BD53-1E8885CF87EF";

        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>(), timeToLiveInSeconds))
            .ReturnsAsync(gatewayReference);

        Mock<ISmsSendResultDispatcher> dispatcherMock = new();
        dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);

        var sendingService = new SendingService(clientMock.Object, dispatcherMock.Object);

        // Act
        await sendingService.SendAsync(sms, timeToLiveInSeconds);

        // Assert
        dispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == notificationId &&
                r.GatewayReference == gatewayReference &&
                r.SendResult == SmsSendResult.Accepted)),
            Times.Once);
        dispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_DefaultTimeToLive_GatewayReferenceGenerated_SendingAccepted()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        const string gatewayReference = "gateway-reference";
        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
            .ReturnsAsync(gatewayReference);

        Mock<ISmsSendResultDispatcher> dispatcherMock = new();
        dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);

        var sendingService = new SendingService(clientMock.Object, dispatcherMock.Object);

        // Act
        await sendingService.SendAsync(sms);

        // Assert
        dispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == notificationId &&
                r.GatewayReference == gatewayReference &&
                r.SendResult == SmsSendResult.Accepted)),
            Times.Once);
        dispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_CustomTimeToLive_InvalidRecipient_DispatchesFailureResult()
    {
        // Arrange
        var timeToLiveInSeconds = 12600;
        Guid notificationId = Guid.NewGuid();
        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>(), timeToLiveInSeconds))
            .ReturnsAsync(new SmsClientErrorResponse { SendResult = SmsSendResult.Failed_InvalidRecipient, ErrorMessage = "Receiver is invalid" });

        Mock<ISmsSendResultDispatcher> dispatcherMock = new();
        dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);

        var sendingService = new SendingService(clientMock.Object, dispatcherMock.Object);

        // Act
        await sendingService.SendAsync(sms, timeToLiveInSeconds);

        // Assert
        dispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == notificationId &&
                r.GatewayReference == string.Empty &&
                r.SendResult == SmsSendResult.Failed_InvalidRecipient)),
            Times.Once);
        dispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_DefaultTimeToLive_InvalidRecipient_DispatchesFailureResult()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        Notifications.Sms.Core.Sending.Sms sms = new(notificationId, "sender", "recipient", "message");

        Mock<ISmsClient> clientMock = new();
        clientMock.Setup(c => c.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
            .ReturnsAsync(new SmsClientErrorResponse { SendResult = SmsSendResult.Failed_InvalidRecipient, ErrorMessage = "Receiver is invalid" });

        Mock<ISmsSendResultDispatcher> dispatcherMock = new();
        dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);

        var sendingService = new SendingService(clientMock.Object, dispatcherMock.Object);

        // Act
        await sendingService.SendAsync(sms);

        // Assert
        dispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == notificationId &&
                r.GatewayReference == string.Empty &&
                r.SendResult == SmsSendResult.Failed_InvalidRecipient)),
            Times.Once);
        dispatcherMock.VerifyNoOtherCalls();
    }
}
