using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;
public class EmailNotificationServiceTests
{
    private const string _emailQueueTopicName = "email.queue";
    private readonly Email _email = new(Guid.NewGuid(), "email.subject", "email.body", "from@domain.com", "to@domain.com", Altinn.Notifications.Core.Enums.EmailContentType.Plain);

    [Fact]
    public async Task SendNotifications_ProducerCalledOnceForEachRetrievedEmail()
    {
        // Arrange 
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotifications())
            .ReturnsAsync(new List<Email>() { _email, _email, _email });

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_emailQueueTopicName)), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications();

        // Assert
        repoMock.Verify();
        producerMock.Verify(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_emailQueueTopicName)), It.IsAny<string>()), Times.Exactly(3));
    }


    [Fact]
    public async Task SendNotifications_ProducerReturnsFalse_RepositoryCalledToUpdateDB()
    {
        // Arrange 
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotifications())
            .ReturnsAsync(new List<Email>() { _email });

        repoMock
            .Setup(r => r.SetResultStatus(It.IsAny<Guid>(), It.Is<EmailNotificationResultType>(t => t == EmailNotificationResultType.New)));

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_emailQueueTopicName)), It.IsAny<string>()))
            .ReturnsAsync(false);

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications();

        // Assert
        repoMock.Verify();
        producerMock.VerifyAll();
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task CreateEmailNotification_ToAddressDefined_ResultNew()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = requestedSendTime.AddHours(1);

        EmailNotification expected = new()
        {
            Id = id,
            OrderId = orderId,
            RecipientId = "skd",
            RequestedSendTime = requestedSendTime,
            SendResult = new(EmailNotificationResultType.New, dateTimeOutput),
            ToAddress = "skd@norge.no"
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient("skd", new List<IAddressPoint>() { new EmailAddressPoint("skd@norge.no") }));

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)), Times.Once);
    }

    [Fact]
    public async Task CreateEmailNotification_ToAddressDefined_ResultFailedRecipientNotDefined()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = dateTimeOutput;

        EmailNotification expected = new()
        {
            Id = id,
            OrderId = orderId,
            RecipientId = "skd",
            RequestedSendTime = requestedSendTime,
            SendResult = new(EmailNotificationResultType.Failed_RecipientNotIdentified, dateTimeOutput),
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient("skd", new List<IAddressPoint>()));

        // Assert
        repoMock.Verify();
    }

    private static EmailNotificationService GetTestService(IEmailNotificationRepository? repo = null, IKafkaProducer? producer = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null)
    {
        var guidService = new Mock<IGuidService>();
        guidService
            .Setup(g => g.NewGuid())
            .Returns(guidOutput ?? Guid.NewGuid());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService
            .Setup(d => d.UtcNow())
            .Returns(dateTimeOutput ?? DateTime.UtcNow);
        if (repo == null)
        {
            var _repo = new Mock<IEmailNotificationRepository>();
            repo = _repo.Object;
        }

        if (producer == null)
        {
            var _producer = new Mock<IKafkaProducer>();
            producer = _producer.Object;
        }

        return new EmailNotificationService(guidService.Object, dateTimeService.Object, repo, producer, Options.Create(new KafkaSettings { EmailQueueTopicName = _emailQueueTopicName }));
    }
}
