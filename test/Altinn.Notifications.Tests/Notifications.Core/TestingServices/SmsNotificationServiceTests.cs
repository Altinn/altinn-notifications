using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using static Confluent.Kafka.ConfigPropertyNames;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class SmsNotificationServiceTests
{
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
        await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, new Recipient("recipientId", new List<IAddressPoint>() { new SmsAddressPoint("999999999") }));

        // Assert
        repoMock.Verify(r => r.AddNotification(It.IsAny<SmsNotification>(), It.IsAny<DateTime>()), Times.Once);
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
            RecipientNumber = "+4799999999",
            SendResult = new(SmsNotificationResultType.New, dateTimeOutput),
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") }));

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_RecipientNumberMissing_ResultFailedRecipientNotDefined()
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
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, new Recipient(new List<IAddressPoint>()));

        // Assert
        repoMock.Verify();
    }

    private static SmsNotificationService GetTestService(ISmsNotificationRepository? repo = null, IKafkaProducer? producer = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null)
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

        return new SmsNotificationService(guidService.Object, dateTimeService.Object, repo, producer, Options.Create(new KafkaSettings()));
    }
}
