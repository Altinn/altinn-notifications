using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Core.Status;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Core.Sending
{
    public class SendingServiceTests
    {
        private readonly TopicSettings _topicSettings;

        public SendingServiceTests()
        {
            _topicSettings = new()
            {
                EmailStatusUpdatedTopicName = "EmailStatusUpdatedTopicName",
                AltinnServiceUpdateTopicName = "AltinnServiceUpdateTopicName",
                EmailSendingAcceptedTopicName = "EmailSendingAcceptedTopicName"
            };
        }

        [Fact]
        public async Task SendAsync_OperationIdentifierGenerated_DelegatedToSendingAcceptedPublisher()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            Notifications.Email.Core.Sending.Email email =
            new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

            Mock<IEmailServiceClient> clientMock = new();
            clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
                .ReturnsAsync("operation-id");

            Mock<ICommonProducer> producerMock = new();
            Mock<IEmailStatusCheckDispatcher> sendingAcceptedPublisherMock = new();
            sendingAcceptedPublisherMock
                .Setup(p => p.DispatchAsync(id, "operation-id"))
                .Returns(Task.CompletedTask);

            Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();

            var sendingService = new SendingService(_topicSettings, producerMock.Object, clientMock.Object, sendingAcceptedPublisherMock.Object, statusDispatcherMock.Object);

            // Act
            await sendingService.SendAsync(email);

            // Assert
            sendingAcceptedPublisherMock.Verify(p => p.DispatchAsync(id, "operation-id"), Times.Once);

            producerMock.VerifyNoOtherCalls();
            statusDispatcherMock.VerifyNoOtherCalls();
            sendingAcceptedPublisherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SendAsync_InvalidEmailAddress_DispatchesStatusResult()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            Notifications.Email.Core.Sending.Email email =
            new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

            Mock<IEmailServiceClient> clientMock = new();
            clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
                .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_InvalidEmailFormat });

            Mock<ICommonProducer> producerMock = new();
            Mock<IEmailStatusCheckDispatcher> sendingAcceptedPublisherMock = new();
            Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
            statusDispatcherMock
                .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
                .Returns(Task.CompletedTask);

            var sendingService = new SendingService(_topicSettings, producerMock.Object, clientMock.Object, sendingAcceptedPublisherMock.Object, statusDispatcherMock.Object);

            // Act
            await sendingService.SendAsync(email);

            // Assert
            statusDispatcherMock.Verify(
                d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                    r.NotificationId == id &&
                    r.OperationId == string.Empty &&
                    r.SendResult == EmailSendResult.Failed_InvalidEmailFormat)),
                Times.Once);

            producerMock.VerifyNoOtherCalls();
            statusDispatcherMock.VerifyNoOtherCalls();
            sendingAcceptedPublisherMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(EmailSendResult.Failed)]
        [InlineData(EmailSendResult.Failed_Bounced)]
        [InlineData(EmailSendResult.Failed_Quarantined)]
        [InlineData(EmailSendResult.Failed_FilteredSpam)]
        [InlineData(EmailSendResult.Failed_SupressedRecipient)]
        public async Task SendAsync_NonTransientFailure_DispatchesStatusResultWithCorrectValues(EmailSendResult failResult)
        {
            // Arrange
            Guid id = Guid.NewGuid();
            Notifications.Email.Core.Sending.Email email =
            new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

            Mock<IEmailServiceClient> clientMock = new();
            clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
                .ReturnsAsync(new EmailClientErrorResponse { SendResult = failResult });

            Mock<ICommonProducer> producerMock = new();
            Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
            Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
            statusDispatcherMock
                .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
                .Returns(Task.CompletedTask);

            var sendingService = new SendingService(_topicSettings, producerMock.Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object);

            // Act
            await sendingService.SendAsync(email);

            // Assert
            statusDispatcherMock.Verify(
                d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                    r.NotificationId == id &&
                    r.OperationId == string.Empty &&
                    r.SendResult == failResult)),
                Times.Once);

            producerMock.VerifyNoOtherCalls();
            checkDispatcherMock.VerifyNoOtherCalls();
            statusDispatcherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SendAsync_Failed_TransientError_PublishesServiceUpdateToKafkaAndDispatchesStatusResult()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            Notifications.Email.Core.Sending.Email email =
            new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

            Mock<IEmailServiceClient> clientMock = new();
            clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
                .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_TransientError, IntermittentErrorDelay = 1000 });

            Mock<ICommonProducer> producerMock = new();
            producerMock
                .Setup(p => p.ProduceAsync(
                    It.Is<string>(s => s.Equals(_topicSettings.AltinnServiceUpdateTopicName)),
                    It.Is<string>(s =>
                        s.Contains("\"source\":\"platform-notifications-email\"") &&
                        s.Contains("\"schema\":\"ResourceLimitExceeded\""))))
                .ReturnsAsync(true);

            Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
            Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
            statusDispatcherMock
                .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
                .Returns(Task.CompletedTask);

            var sendingService = new SendingService(_topicSettings, producerMock.Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object);

            // Act
            await sendingService.SendAsync(email);

            // Assert - service update goes to Kafka
            producerMock.Verify(
                p => p.ProduceAsync(
                    _topicSettings.AltinnServiceUpdateTopicName,
                    It.Is<string>(s => s.Contains("ResourceLimitExceeded"))),
                Times.Once);
            producerMock.VerifyNoOtherCalls();

            // Assert - status result goes via dispatcher
            statusDispatcherMock.Verify(
                d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                    r.NotificationId == id &&
                    r.OperationId == string.Empty &&
                    r.SendResult == EmailSendResult.Failed_TransientError)),
                Times.Once);

            checkDispatcherMock.VerifyNoOtherCalls();
            statusDispatcherMock.VerifyNoOtherCalls();
        }
    }
}
