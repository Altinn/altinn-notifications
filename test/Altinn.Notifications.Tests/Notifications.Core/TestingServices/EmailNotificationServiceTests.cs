using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
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
    private readonly Email _email = new(Guid.NewGuid().ToString(), "email.subject", "email.body", "from@domain.com", "to@domain.com", Altinn.Notifications.Core.Enums.EmailContentType.Plain);

    [Fact]
    public async Task SendNotifications_ProducerCalledOnceForEachRetrievedEmail()
    {
        // Arrange 
        var repoMock = new Mock<IEmailNotificationsRepository>();
        repoMock.Setup(r => r.GetNewNotifications())
            .ReturnsAsync(new List<Email>() { _email, _email, _email });

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_emailQueueTopicName)), It.IsAny<string>()));

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications();

        // Assert
        repoMock.Verify();
        producerMock.Verify(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_emailQueueTopicName)), It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task CreateEmailNotification_ToAddressDefined_ResultNew()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = requestedSendTime.AddHours(1);

        EmailNotification expected = new()
        {
            Id = id,
            OrderId = "orderid",
            RecipientId = "skd",
            RequestedSendTime = requestedSendTime,
            SendResult = new(Altinn.Notifications.Core.Enums.EmailNotificationResultType.New, dateTimeOutput),
            ToAddress = "skd@norge.no"
        };

        var repoMock = new Mock<IEmailNotificationsRepository>();
        repoMock.Setup(r => r.AddEmailNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification("orderid", requestedSendTime, new Recipient("skd", new List<IAddressPoint>() { new EmailAddressPoint("skd@norge.no") }));

        // Assert
        repoMock.Verify();
    }

    [Fact]
    public async Task CreateEmailNotification_ToAddressDefined_ResultFailedRecipientNotDefined()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = dateTimeOutput;

        EmailNotification expected = new()
        {
            Id = id,
            OrderId = "orderid",
            RecipientId = "skd",
            RequestedSendTime = requestedSendTime,
            SendResult = new(Altinn.Notifications.Core.Enums.EmailNotificationResultType.Failed_RecipientNotIdentified, dateTimeOutput),
        };

        var repoMock = new Mock<IEmailNotificationsRepository>();
        repoMock.Setup(r => r.AddEmailNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification("orderid", requestedSendTime, new Recipient("skd", new List<IAddressPoint>()));

        // Assert
        repoMock.Verify();
    }

    private static EmailNotificationService GetTestService(IEmailNotificationsRepository? repo = null, IKafkaProducer? producer = null, string? guidOutput = null, DateTime? dateTimeOutput = null)
    {
        var guidService = new Mock<IGuidService>();
        guidService
            .Setup(g => g.NewGuidAsString())
            .Returns(guidOutput ?? Guid.NewGuid().ToString());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService
            .Setup(d => d.UtcNow())
            .Returns(dateTimeOutput ?? DateTime.UtcNow);
        if (repo == null)
        {
            var _repo = new Mock<IEmailNotificationsRepository>();
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
