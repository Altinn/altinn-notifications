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

public class SmsNotificationServiceTests
{
    private const string _smsQueueTopicName = "test.sms.queue";
    private readonly Sms _sms = new(Guid.NewGuid(), "Altinn Test", "Recipient", "Text message");

    [Fact]
    public async Task CreateNotifications_NewSmsNotification_RepositoryCalledOnce()
    {
        // Arrange 
        var repoMock = new Mock<ISmsNotificationRepository>();
        var guidService = new Mock<IGuidService>();
        guidService
            .Setup(g => g.NewGuid())
            .Returns(Guid.NewGuid());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService
            .Setup(d => d.UtcNow())
            .Returns(DateTime.UtcNow);

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("999999999") }, nationalIdentityNumber: "enduser-nin"), It.IsAny<int>());

        // Assert
        repoMock.Verify(r => r.AddNotification(It.IsAny<SmsNotification>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_RecipientNumberIsDefined_ResultNew()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = requestedSendTime.AddHours(1);

        SmsNotification expected = new()
        {
            Id = id,
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = "+4799999999"
            },
            SendResult = new(SmsNotificationResultType.New, dateTimeOutput),
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") }), 1);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()), Times.Once);
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

        SmsNotification expected = new()
        {
            Id = id,
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                IsReserved = true
            },
            SendResult = new(SmsNotificationResultType.Failed_RecipientReserved, dateTimeOutput),
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient() { IsReserved = true }, 1);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()), Times.Once);
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

        SmsNotification expected = new()
        {
            Id = id,
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                IsReserved = true,
                MobileNumber = "+4799999999"
            },
            SendResult = new(SmsNotificationResultType.New, dateTimeOutput),
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient() { IsReserved = true, AddressInfo = [new SmsAddressPoint("+4799999999")] }, 1, true);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_RecipientNumberMissing_LookupFails_ResultFailedRecipientNotDefined()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = dateTimeOutput;

        SmsNotification expected = new()
        {
            Id = id,
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            SendResult = new(SmsNotificationResultType.Failed_RecipientNotIdentified, dateTimeOutput),
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient(new List<IAddressPoint>()), It.IsAny<int>());

        // Assert
        repoMock.Verify();
    }

    [Fact]
    public async Task CreateNotification_RecipientHasTwoMobileNumbers_RepositoryCalledOnceForEachNumber()
    {
        // Arrange        
        Recipient recipient = new()
        {
            OrganizationNumber = "org",
            AddressInfo = [new SmsAddressPoint("+4748123456"), new SmsAddressPoint("+4799123456")]
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>(), It.IsAny<int>()));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, recipient, 1, true);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<SmsNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SendNotifications_ProducerCalledOnceForEachRetrievedSms()
    {
        // Arrange 
        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotifications())
            .ReturnsAsync(new List<Sms>() { _sms, _sms, _sms });

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_smsQueueTopicName)), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications();

        // Assert
        repoMock.Verify();
        producerMock.Verify(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_smsQueueTopicName)), It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task SendNotifications_ProducerReturnsFalse_RepositoryCalledToUpdateDB()
    {
        // Arrange 
        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotifications())
            .ReturnsAsync(new List<Sms>() { _sms });

        repoMock
            .Setup(r => r.UpdateSendStatus(It.IsAny<Guid>(), It.Is<SmsNotificationResultType>(t => t == SmsNotificationResultType.New), It.IsAny<string?>()));

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_smsQueueTopicName)), It.IsAny<string>()))
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
    public async Task UpdateSendStatus_SendResultDefined_Succeded()
    {
        // Arrange
        Guid notificationid = Guid.NewGuid();
        string gatewayReference = Guid.NewGuid().ToString();

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationid,
            SendResult = SmsNotificationResultType.Accepted,
            GatewayReference = gatewayReference
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(
            It.Is<Guid>(n => n == notificationid),
            It.Is<SmsNotificationResultType>(e => e == SmsNotificationResultType.Accepted),
            It.Is<string>(s => s.Equals(gatewayReference))));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify();
    }

    private static SmsNotificationService GetTestService(ISmsNotificationRepository? repo = null, IKafkaProducer? producer = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null, IKeywordsService? keywordsService = null)
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
            var repoMock = new Mock<ISmsNotificationRepository>();
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
            //keywordsServiceMock.Setup(e => e.ReplaceKeywordsAsync(It.IsAny<SmsRecipient>())).ReturnsAsync((SmsRecipient recipient) => recipient);
            //keywordsService = keywordsServiceMock.Object;
        }

        return new SmsNotificationService(guidService.Object, dateTimeService.Object, repo, producer, Options.Create(new KafkaSettings { SmsQueueTopicName = _smsQueueTopicName }), keywordsService);
    }
}
