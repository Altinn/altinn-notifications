using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class EmailNotificationServiceTests
{
    private const string _emailQueueTopicName = "email.queue";
    private readonly Email _email = new(Guid.NewGuid(), "email.subject", "email.body", "from@domain.com", "to@domain.com", EmailContentType.Plain);

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
            .Setup(r => r.UpdateSendStatus(It.IsAny<Guid>(), It.Is<EmailNotificationResultType>(t => t == EmailNotificationResultType.New), It.IsAny<string?>()));

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
    public async Task CreateNotification_ToAddressDefined_ResultNew()
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
            Recipient = new()
            {
                OrganizationNumber = "skd-orgno",
                ToAddress = "skd@norge.no"
            },
            RequestedSendTime = requestedSendTime,
            SendResult = new(EmailNotificationResultType.New, dateTimeOutput)
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient(new List<IAddressPoint>() { new EmailAddressPoint("skd@norge.no") }, organizationNumber: "skd-orgno"));

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_RecipientIsReserved_IgnoreReservationsFalse_ResultFailedRecipientReserved()
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
            Recipient = new()
            {
                IsReserved = true
            },
            RequestedSendTime = requestedSendTime,
            SendResult = new(EmailNotificationResultType.Failed_RecipientReserved, dateTimeOutput)
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient() { IsReserved = true });

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_RecipientIsReserved_IgnoreReservationsTrue_ResultNew()
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
            Recipient = new()
            {
                IsReserved = true,
                ToAddress = "email@domain.com"
            },
            RequestedSendTime = requestedSendTime,
            SendResult = new(EmailNotificationResultType.New, dateTimeOutput)
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient() { IsReserved = true, AddressInfo = [new EmailAddressPoint("email@domain.com")] }, true);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_ToAddressMissing_LookupFails_ResultFailedRecipientNotDefined()
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
            Recipient = new()
            {
                OrganizationNumber = "skd-orgno"
            },
            RequestedSendTime = requestedSendTime,
            SendResult = new(EmailNotificationResultType.Failed_RecipientNotIdentified, dateTimeOutput),
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient(new List<IAddressPoint>(), organizationNumber: "skd-orgno"));

        // Assert
        repoMock.Verify();
    }

    [Fact]
    public async Task CreateNotification_RecipientHasTwoEmailAddresses_RepositoryCalledOnceForEachAddress()
    {
        // Arrange        
        Recipient recipient = new()
        {
            OrganizationNumber = "org",
            AddressInfo = [new EmailAddressPoint("user_1@domain.com"), new EmailAddressPoint("user_2@domain.com")]
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>()));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, recipient);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<EmailNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateSendStatus_SendResultDefined_Succeded()
    {
        // Arrange
        Guid notificationid = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationid,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Succeeded
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(It.Is<Guid>(n => n == notificationid), It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded), It.Is<string>(s => s.Equals(operationId))));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify();
    }

    [Fact]
    public async Task UpdateSendStatus_TransientErrorResult_ConvertedToNew()
    {
        // Arrange
        Guid notificationid = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationid,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Failed_TransientError
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(
            It.Is<Guid>(n => n == notificationid),
            It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.New),
            It.Is<string>(s => s.Equals(operationId))));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify();
    }

    private static EmailNotificationService GetTestService(IEmailNotificationRepository? repo = null, IKafkaProducer? producer = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null, IKeywordsService? keywordsService = null)
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
            var repoMock = new Mock<IEmailNotificationRepository>();
            repo = repoMock.Object;
        }

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        if (keywordsService == null)
        {
            //var keywordsServiceMock = new Mock<IKeywordsService>();
            //keywordsServiceMock.Setup(e => e.ReplaceKeywordsAsync(It.IsAny<EmailRecipient>())).ReturnsAsync((EmailRecipient recipient) => recipient);
            //keywordsService = keywordsServiceMock.Object;
        }

        return new EmailNotificationService(guidService.Object, dateTimeService.Object, repo, producer, Options.Create(new KafkaSettings { EmailQueueTopicName = _emailQueueTopicName }), keywordsService);
    }
}
