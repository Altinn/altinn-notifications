using Altinn.Notifications.Email.Core.Configuration;
using Altinn.Notifications.Email.Core.Dependencies;
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
                EmailStatusUpdatedTopicName = "EmailStatusUpdatedTopicName"
            };
        }

        [Fact]
        public async Task SendAsync_OperationIdentifierGenerated_PublishedToExpectedKafkaTopic()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            Notifications.Email.Core.Sending.Email email =
            new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

            Mock<IEmailServiceClient> clientMock = new();
            clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
                .ReturnsAsync("operation-id");

            Mock<ICommonProducer> producerMock = new();
            producerMock.Setup(p => p.ProduceAsync(
                It.Is<string>(s => s.Equals(nameof(_topicSettings.EmailSendingAcceptedTopicName))),
                It.Is<string>(s =>
                s.Contains("\"operationId\":\"operation-id\"") &&
                s.Contains($"\"notificationId\":\"{id}\""))));

            var sut = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

            // Act
            await sut.SendAsync(email);

            // Assert
            producerMock.VerifyAll();
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
                .ReturnsAsync(EmailSendResult.Failed_InvalidEmailFormat);

            Mock<ICommonProducer> producerMock = new();
            producerMock.Setup(p => p.ProduceAsync(
                It.Is<string>(s => s.Equals(nameof(_topicSettings.EmailStatusUpdatedTopicName))),
                It.Is<string>(s =>
                s.Contains($"\"notificationId\":\"{id}\"") &&
                s.Contains("\"sendResult\":\"Failed_InvalidEmailFormat\""))));

            var sut = new SendingService(clientMock.Object, producerMock.Object, _topicSettings);

            // Act
            await sut.SendAsync(email);

            // Assert
            producerMock.VerifyAll();
        }
    }
}
