using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public async Task CreateNotification_RecipientNumberIsDefined_ResultNew()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = requestedSendTime.AddHours(48);

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

        var service = GetTestService(repository: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, requestedSendTime.AddHours(48), [new("+4799999999")], new SmsRecipient(), 1);

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
        DateTime expectedExpiry = requestedSendTime.AddHours(48);

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

        var service = GetTestService(repository: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, requestedSendTime.AddHours(48), [], new SmsRecipient { IsReserved = true }, 1);

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
        DateTime expectedExpiry = requestedSendTime.AddHours(48);

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

        var service = GetTestService(repository: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, requestedSendTime.AddHours(48), [new("+4799999999")], new SmsRecipient { IsReserved = true }, 1, true);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<SmsNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CreateNotification_RecipientNumberMissing_LookupFails_ResultFailedRecipientNotIdentified()
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

        var service = GetTestService(repository: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, requestedSendTime.AddHours(48), [], new SmsRecipient(), 1);

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
            AddressInfo = new List<IAddressPoint> { new SmsAddressPoint("+4748123456"), new SmsAddressPoint("+4799123456") }
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<SmsNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>(), It.IsAny<int>()));

        var service = GetTestService(repository: repoMock.Object);

        // Act
        await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(48), recipient.AddressInfo.OfType<SmsAddressPoint>().ToList(), new SmsRecipient { OrganizationNumber = "org" }, 1, true);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<SmsNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Exactly(2));
    }

    //[Fact]
    //public async Task SendNotifications_MultipleBatches_AllProduced()
    //{
    //    // Arrange
    //    var firstBatch = new List<Sms>
    //    {
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 1"),
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 2")
    //    };

    //    var secondBatch = new List<Sms>
    //    {
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 3")
    //    };

    //    var thirdBatch = new List<Sms>
    //    {
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 4"),
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 5"),
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 6"),
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 7"),
    //        new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 8")
    //    };

    //    int allBatches = firstBatch.Count + secondBatch.Count + thirdBatch.Count;

    //    var repoMock = new Mock<ISmsNotificationRepository>();
    //    repoMock.SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime))
    //        .ReturnsAsync(firstBatch)
    //        .ReturnsAsync(secondBatch)
    //        .ReturnsAsync(thirdBatch)
    //        .ReturnsAsync([]);

    //    var producerMock = new Mock<IKafkaProducer>();
    //    producerMock.Setup(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()))
    //        .ReturnsAsync(true);

    //    var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object, publishBatchSize: 1);

    //    // Act
    //    await service.SendNotifications(CancellationToken.None, SendingTimePolicy.Anytime);

    //    // Assert
    //    producerMock.Verify(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()), Times.Exactly(allBatches));
    //    repoMock.Verify(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime), Times.Exactly(4));
    //}

    [Fact]
    public async Task SendNotifications_ProducerThrows_StatusResetToNew()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification");

        var repoMock = new Mock<ISmsNotificationRepository>();

        repoMock.SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync([sms])
            .ReturnsAsync([]);

        repoMock.Setup(r => r.UpdateSendStatus(sms.NotificationId, SmsNotificationResultType.New, null));

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(sms.NotificationId, SmsNotificationResultType.New, null), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_ProduceReturnedFalse_StatusResetToNew()
    {
        // Arrange
        var repoMock = new Mock<ISmsNotificationRepository>();

        repoMock.SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()))
            .ReturnsAsync([_sms])
            .ReturnsAsync([]);

        repoMock.Setup(r => r.UpdateSendStatus(It.Is<Guid>(g => g == _sms.NotificationId), SmsNotificationResultType.New, It.IsAny<string?>()));

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()))
            .ReturnsAsync(false);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(_sms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(r => r.GetNewNotifications(It.Is<int>(b => b == 50), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Exactly(1));
    }

    [Fact]
    public async Task SendNotifications_SingleBatchThenEmpty_ProducesEachAndStops()
    {
        // Arrange
        List<Sms> firstBatch = [_sms, _sms, _sms];

        var repositoryMock = new Mock<ISmsNotificationRepository>();

        // First call returns 3, second returns empty => loop stops
        repositoryMock.SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(firstBatch)
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(e => e.ProduceAsync(_smsQueueTopicName, It.IsAny<string>())).ReturnsAsync(true);

        var service = GetTestService(repository: repositoryMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()), Times.Exactly(firstBatch.Count));
        repositoryMock.Verify(r => r.GetNewNotifications(It.Is<int>(b => b == 50), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Exactly(1));
    }

    [Fact]
    public async Task SendNotifications_PublishBatchSize_ConfiguredValuePassedToRepository()
    {
        // Arrange
        const int customBatchSize = 7;

        var repoMock = new Mock<ISmsNotificationRepository>();

        // Return empty immediately to end loop
        repoMock.Setup(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object, publishBatchSize: customBatchSize);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        repoMock.Verify(r => r.GetNewNotifications(It.Is<int>(b => b == customBatchSize), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_MixedSuccessFailureAndException_CorrectStatusUpdates()
    {
        // Arrange
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Ok");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Will throw");
        var thirdSms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Returns false");

        var batch = new List<Sms> { firstSms, secondSms, thirdSms };

        var repoMock = new Mock<ISmsNotificationRepository>();

        repoMock.SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        repoMock.Setup(r => r.UpdateSendStatus(secondSms.NotificationId, SmsNotificationResultType.New, null));
        repoMock.Setup(r => r.UpdateSendStatus(thirdSms.NotificationId, SmsNotificationResultType.New, null));

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.SetupSequence(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()))
            .ReturnsAsync(true)
            .ThrowsAsync(new Exception("Intermittent"))
            .ReturnsAsync(false);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()), Times.Exactly(3));

        // Failures only
        repoMock.Verify(r => r.UpdateSendStatus(secondSms.NotificationId, SmsNotificationResultType.New, null), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(thirdSms.NotificationId, SmsNotificationResultType.New, null), Times.Once);

        // No update for success
        repoMock.Verify(r => r.UpdateSendStatus(firstSms.NotificationId, It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_DefaultSendingTimePolicy_DaytimeUsed_NoStatusUpdatesOnSuccess()
    {
        // Arrange
        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync([_sms, _sms])
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None); // rely on default parameter

        // Assert
        producerMock.Verify(e => e.ProduceAsync(_smsQueueTopicName, It.IsAny<string>()), Times.Exactly(2));
        repoMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>()), Times.Never);
        repoMock.Verify(e => e.GetNewNotifications(It.Is<int>(e => e == 50), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Exactly(1));
    }

    [Fact]
    public async Task UpdateSendStatus_SendResultDefined_Succeeded()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        string gatewayReference = Guid.NewGuid().ToString();

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            SendResult = SmsNotificationResultType.Accepted,
            GatewayReference = gatewayReference
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(
            It.Is<Guid>(n => n == notificationId),
            It.Is<SmsNotificationResultType>(e => e == SmsNotificationResultType.Accepted),
            It.Is<string>(s => s.Equals(gatewayReference))));

        var service = GetTestService(repository: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify();
    }

    private static SmsNotificationService GetTestService(
        Guid? guidOutput = null,
        DateTime? dateTimeOutput = null,
        IKafkaProducer? producer = null,
        ISmsNotificationRepository? repository = null,
        int? publishBatchSize = null)
    {
        var guidService = MockGuidService(guidOutput);
        var dateTimeService = MockDateTimeService(dateTimeOutput);

        producer ??= new Mock<IKafkaProducer>().Object;
        repository ??= new Mock<ISmsNotificationRepository>().Object;

        return new SmsNotificationService(
            guidService,
            producer,
            dateTimeService,
            repository,
            Options.Create(new KafkaSettings { SmsQueueTopicName = _smsQueueTopicName }),
            Options.Create(new NotificationConfig { SmsPublishBatchSize = publishBatchSize ?? 50 }));
    }

    private static IGuidService MockGuidService(Guid? guidOutput)
    {
        var mock = new Mock<IGuidService>();
        mock.Setup(g => g.NewGuid()).Returns(guidOutput ?? Guid.NewGuid());
        return mock.Object;
    }

    private static IDateTimeService MockDateTimeService(DateTime? dateTimeOutput)
    {
        var mock = new Mock<IDateTimeService>();
        mock.Setup(d => d.UtcNow())
            .Returns(dateTimeOutput ?? DateTime.UtcNow);
        return mock.Object;
    }
}
