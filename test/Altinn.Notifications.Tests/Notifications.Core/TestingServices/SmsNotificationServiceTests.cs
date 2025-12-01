using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

    [Fact]
    public async Task SendNotifications_MultipleBatches_AllPublishedInBatches()
    {
        // Arrange
        var firstBatch = new List<Sms>
        {
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 1"),
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 2")
        };

        var secondBatch = new List<Sms>
        {
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 3")
        };

        var thirdBatch = new List<Sms>
        {
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 4"),
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 5"),
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 6"),
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 7"),
            new(Guid.NewGuid(), "Altinn", "+4799999999", "SMS notification 8")
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime))
            .ReturnsAsync(firstBatch)
            .ReturnsAsync(secondBatch)
            .ReturnsAsync(thirdBatch)
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(
                _smsQueueTopicName,
                It.IsAny<ImmutableList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object, publishBatchSize: 1);

        // Act
        await service.SendNotifications(CancellationToken.None, SendingTimePolicy.Anytime);

        // Assert
        producerMock.Verify(
            p => p.ProduceAsync(
                _smsQueueTopicName,
                It.Is<ImmutableList<string>>(m => m.Count == firstBatch.Count),
                It.IsAny<CancellationToken>()),
            Times.Once);

        producerMock.Verify(
            p => p.ProduceAsync(
                _smsQueueTopicName,
                It.Is<ImmutableList<string>>(m => m.Count == secondBatch.Count),
                It.IsAny<CancellationToken>()),
            Times.Once);

        producerMock.Verify(
            p => p.ProduceAsync(
                _smsQueueTopicName,
                It.Is<ImmutableList<string>>(m => m.Count == thirdBatch.Count),
                It.IsAny<CancellationToken>()),
            Times.Once);

        repoMock.Verify(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime), Times.Exactly(4));
    }

    [Fact]
    public async Task SendNotifications_ProducerReturnsAllUnpublished_AllSmsResetToNew()
    {
        // Arrange
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990000", "first");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "second");
        var thirdSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990002", "third");

        var batch = new List<Sms> { firstSms, secondSms, thirdSms };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock
            .SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();

        producerMock
            .Setup(e => e.ProduceAsync(
                _smsQueueTopicName,
                It.Is<ImmutableList<string>>(e => e.Count == batch.Count),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstSms.Serialize(), secondSms.Serialize(), thirdSms.Serialize()]);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        repoMock.Verify(e => e.UpdateSendStatus(firstSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(secondSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(thirdSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_SingleBatchThenEmpty_PublishesOneBatchAndStops()
    {
        // Arrange
        List<Sms> firstBatch = [_sms, _sms, _sms];

        var repositoryMock = new Mock<ISmsNotificationRepository>();

        // First call returns 3, second returns empty => loop stops
        repositoryMock.SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(firstBatch)
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(
                _smsQueueTopicName,
                It.IsAny<ImmutableList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repository: repositoryMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(
            p => p.ProduceAsync(
            _smsQueueTopicName,
            It.Is<ImmutableList<string>>(m => m.Count == firstBatch.Count),
            It.IsAny<CancellationToken>()),
            Times.Once);

        repositoryMock.Verify(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Exactly(2));
    }

    [Fact]
    public async Task SendNotifications_BatchProduceReturnedUnpublished_StatusResetToNew()
    {
        // Arrange
        var repoMock = new Mock<ISmsNotificationRepository>();

        repoMock.SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()))
            .ReturnsAsync([_sms])
            .ReturnsAsync([]);

        repoMock.Setup(r => r.UpdateSendStatus(It.Is<Guid>(g => g == _sms.NotificationId), SmsNotificationResultType.New, It.IsAny<string?>()));

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(
                _smsQueueTopicName,
                It.IsAny<ImmutableList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_sms.Serialize()]);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(
            p => p.ProduceAsync(
            _smsQueueTopicName,
            It.Is<ImmutableList<string>>(m => m.Count == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);

        repoMock.Verify(r => r.UpdateSendStatus(_sms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendNotifications_RepositoryThrowsOperationCanceled_NoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock
            .Setup(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()))
            .ThrowsAsync(new OperationCanceledException()); // Simulate cancellation

        var service = GetTestService(repository: repoMock.Object);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cancellationTokenSource.Token));

        repoMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>()), Times.Never);
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
    public async Task SendNotifications_CancellationAfterProduceOnNextFetch_NoStatusResets()
    {
        // Arrange
        List<Sms> firstBatch = [_sms, _sms];

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock
            .SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Anytime))
            .ReturnsAsync(firstBatch) // First call returns batch
            .ThrowsAsync(new OperationCanceledException()); // Second call simulates cancellation

        var producer = new Mock<IKafkaProducer>();
        producer
            .Setup(e => e.ProduceAsync(_smsQueueTopicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repository: repoMock.Object, producer: producer.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(CancellationToken.None, SendingTimePolicy.Anytime));

        producer.Verify(e => e.ProduceAsync(_smsQueueTopicName, It.Is<ImmutableList<string>>(m => m.Count == firstBatch.Count), It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_ProducerThrowsOperationCanceled_StatusResetForBatch()
    {
        // Arrange
        List<Sms> batch = [_sms, _sms, _sms];

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch);

        var producer = new Mock<IKafkaProducer>();
        producer
            .Setup(e => e.ProduceAsync(_smsQueueTopicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = GetTestService(repository: repo.Object, producer: producer.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(CancellationToken.None));

        repo.Verify(e => e.UpdateSendStatus(It.Is<Guid>(id => batch.Exists(s => s.NotificationId == id)), SmsNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(batch.Count));
    }

    [Fact]
    public async Task SendNotifications_CancellationAfterFetchBeforeProduce_StatusResetForBatch()
    {
        // Arrange
        List<Sms> batch = [_sms, _sms, _sms];

        using var cancellationTokenSource = new CancellationTokenSource();

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .Callback(cancellationTokenSource.Cancel)
            .ReturnsAsync(batch);

        var producer = new Mock<IKafkaProducer>();

        var service = GetTestService(repository: repo.Object, producer: producer.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cancellationTokenSource.Token));

        repo.Verify(r => r.UpdateSendStatus(It.Is<Guid>(id => batch.Exists(s => s.NotificationId == id)), SmsNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(batch.Count));
        producer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_ProducerReturnsSubsetUnpublished_OnlyFailedSmsResetToNew()
    {
        // Arrange
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "first");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "second");
        var thirdSms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "third");

        var batch = new List<Sms> { firstSms, secondSms, thirdSms };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock
            .SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();

        producerMock
            .Setup(e => e.ProduceAsync(
                _smsQueueTopicName,
                It.Is<ImmutableList<string>>(e => e.Count == batch.Count),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([secondSms.Serialize()]);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(e => e.ProduceAsync(_smsQueueTopicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(e => e.UpdateSendStatus(secondSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(firstSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Never);
        repoMock.Verify(e => e.UpdateSendStatus(thirdSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_DefaultPolicy_Daytime_BatchPublished_NoStatusUpdatesOnSuccess()
    {
        // Arrange
        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync([_sms, _sms])
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(
                _smsQueueTopicName,
                It.IsAny<ImmutableList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(
            p => p.ProduceAsync(
            _smsQueueTopicName,
            It.Is<ImmutableList<string>>(m => m.Count == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);

        repoMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>()), Times.Never);
        repoMock.Verify(e => e.GetNewNotifications(It.Is<int>(b => b == 50), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateSendStatus_WithValidNotificationAndGatewayReference_DelegatesToRepository()
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

    [Fact]
    public async Task SendNotifications_ProducerReturnsInvalidAndValidUnpublished_OnlyValidSmsResetToNew()
    {
        // Arrange
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4744444444", "first");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4755555555", "second");
        var thirdSms = new Sms(Guid.NewGuid(), "Altinn", "+4766666666", "third");
        var batch = new List<Sms> { firstSms, secondSms, thirdSms };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock
            .SetupSequence(e => e.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();
        var invalidEntries = new[] { "{}" };
        producerMock
            .Setup(e => e.ProduceAsync(
                _smsQueueTopicName,
                It.Is<ImmutableList<string>>(e => e.Count == batch.Count),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstSms.Serialize(), thirdSms.Serialize(), .. invalidEntries]);

        var service = GetTestService(repository: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        repoMock.Verify(e => e.UpdateSendStatus(firstSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(thirdSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(secondSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Never);

        producerMock.Verify(p => p.ProduceAsync(_smsQueueTopicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
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
