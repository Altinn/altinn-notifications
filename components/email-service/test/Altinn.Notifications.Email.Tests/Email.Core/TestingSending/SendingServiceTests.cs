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
                EmailSendingAcceptedTopicName = "EmailSendingAcceptedTopicName",
                EmailStatusUpdatedTopicName = "EmailStatusUpdatedTopicName",
                AltinnServiceUpdateTopicName = "AltinnServiceUpdateTopicName"
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

            var sut = new SendingService(_topicSettings, producerMock.Object, clientMock.Object, sendingAcceptedPublisherMock.Object);

            // Act
            await sut.SendAsync(email);

            // Assert
            sendingAcceptedPublisherMock.Verify(p => p.DispatchAsync(id, "operation-id"), Times.Once);
            producerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SendAsync_InvalidEmailAddress_PublishedToExpectedKafkaTopic()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            Notifications.Email.Core.Sending.Email email =
            new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

            Mock<IEmailServiceClient> clientMock = new();
            clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
                .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_InvalidEmailFormat });

            Mock<ICommonProducer> producerMock = new();
            producerMock.Setup(p => p.ProduceAsync(
                It.Is<string>(s => s.Equals(nameof(_topicSettings.EmailStatusUpdatedTopicName))),
                It.Is<string>(s =>
                s.Contains($"\"notificationId\":\"{id}\"") &&
                s.Contains("\"sendResult\":\"Failed_InvalidEmailFormat\""))));

            Mock<IEmailStatusCheckDispatcher> sendingAcceptedPublisherMock = new();

            var sut = new SendingService(_topicSettings, producerMock.Object, clientMock.Object, sendingAcceptedPublisherMock.Object);

            // Act
            await sut.SendAsync(email);

            // Assert
            producerMock.VerifyAll();
            sendingAcceptedPublisherMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SendAsync_Failed_TransientError_PublishedToExpectedKafkaTopics()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            Notifications.Email.Core.Sending.Email email =
            new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

            Mock<IEmailServiceClient> clientMock = new();
            clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
                .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_TransientError, IntermittentErrorDelay = 1000 });

            Mock<ICommonProducer> producerMock = new();

            MockSequence sequence = new();

            producerMock.InSequence(sequence).Setup(p => p.ProduceAsync(
                It.Is<string>(s => s.Equals(nameof(_topicSettings.AltinnServiceUpdateTopicName))),
                It.Is<string>(s =>
                s.Contains("\"source\":\"platform-notifications-email\"") &&
                s.Contains("\"schema\":\"ResourceLimitExceeded\""))));

            producerMock.InSequence(sequence).Setup(p => p.ProduceAsync(
                It.Is<string>(s => s.Equals(nameof(_topicSettings.EmailStatusUpdatedTopicName))),
                It.Is<string>(s =>
                s.Contains($"\"notificationId\":\"{id}\"") &&
                s.Contains("\"sendResult\":\"Failed_TransientError\""))));

            Mock<IEmailStatusCheckDispatcher> sendingAcceptedPublisherMock = new();

            var sut = new SendingService(_topicSettings, producerMock.Object, clientMock.Object, sendingAcceptedPublisherMock.Object);

            // Act
            await sut.SendAsync(email);

            // Assert
            producerMock.VerifyAll();
            sendingAcceptedPublisherMock.VerifyNoOtherCalls();
        }
    }
}
