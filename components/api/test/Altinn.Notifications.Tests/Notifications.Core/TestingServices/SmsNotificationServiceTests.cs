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
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class SmsNotificationServiceTests
{
    private readonly int _publishBatchSize = 500;
    private readonly Sms _sms = new(Guid.NewGuid(), "Altinn Test", "Recipient", "Text message");

    [Fact]
    public async Task UpdateStatus_WhenStatusIsAccepted_ShouldPassStatusIsAcceptedOrSucceededAsTrue()
    {
        // Arrange
        Guid notificationid = Guid.NewGuid();
        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationid,
            SendResult = SmsNotificationResultType.Accepted,
            GatewayReference = Guid.NewGuid().ToString()
        };

        var mockRepo = new Mock<SmsNotificationRepository>(
            (null as NpgsqlDataSource)!,
            (null as ILogger<SmsNotificationRepository>)!,
            Options.Create(new NotificationConfig()))
        {
            CallBase = true
        };

        mockRepo.Protected()
            .Setup<Task>(
                "ExecuteUpdateWithTransactionAsync",
                ItExpr.IsAny<string>(),
                ItExpr.IsAny<Action<NpgsqlCommand>>(),
                ItExpr.IsAny<NotificationChannel>(),
                ItExpr.IsAny<Guid?>(),
                ItExpr.IsAny<string?>(),
                ItExpr.IsAny<bool>(),
                ItExpr.IsAny<SendStatusIdentifierType>())
            .Returns(Task.CompletedTask);

        var service = new SmsNotificationService(
            new Mock<IGuidService>().Object,
            new Mock<IDateTimeService>().Object,
            mockRepo.Object,
            new Mock<ISendSmsPublisher>().Object,
            Options.Create(new NotificationConfig { SmsPublishBatchSize = _publishBatchSize }));

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert - verify ExecuteUpdateWithTransactionAsync was called with statusIsAcceptedOrSucceeded = true
        mockRepo.Protected()
            .Verify<Task>(
                "ExecuteUpdateWithTransactionAsync", 
                Times.Once(),
                ItExpr.IsAny<string>(),
                ItExpr.IsAny<Action<NpgsqlCommand>>(),
                ItExpr.Is<NotificationChannel>(c => c == NotificationChannel.Sms),
                ItExpr.IsAny<Guid?>(),
                ItExpr.IsAny<string?>(),
                ItExpr.Is<bool>(b => b),
                ItExpr.IsAny<SendStatusIdentifierType>());
    }

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
    public async Task SendNotifications_WhenSendViaWolverineEnabled_CallsPublishAsyncWithBatch()
    {
        // Arrange
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "first");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990002", "second");
        var batch = new List<Sms> { firstSms, secondSms };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock
            .SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var commandPublisherMock = new Mock<ISendSmsPublisher>();
        commandPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Sms>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]); // empty list = published successfully
        
        var service = GetTestService(
            repository: repoMock.Object,
            commandPublisher: commandPublisherMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert - PublishAsync called once with a batch containing both SMS items
        commandPublisherMock.Verify(
            p => p.PublishAsync(
                It.Is<IReadOnlyList<Sms>>(list =>
                    list.Any(s => s.NotificationId == firstSms.NotificationId) &&
                    list.Any(s => s.NotificationId == secondSms.NotificationId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotifications_WhenSendViaWolverineEnabled_PublishAsyncFails_StatusResetToNew()
    {
        // Arrange
        var failedSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "failed");
        var succeededSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990002", "succeeded");
        var batch = new List<Sms> { failedSms, succeededSms };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock
            .SetupSequence(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var commandPublisherMock = new Mock<ISendSmsPublisher>();
        commandPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Sms>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([failedSms]); // non-empty list = failed to publish those returned

        var service = GetTestService(
            repository: repoMock.Object,
            commandPublisher: commandPublisherMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert - only the failed SMS has its status reset
        repoMock.Verify(r => r.UpdateSendStatus(failedSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(succeededSms.NotificationId, SmsNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    private static SmsNotificationService GetTestService(
        Guid? guidOutput = null,
        DateTime? dateTimeOutput = null,
        ISmsNotificationRepository? repository = null,
        ISendSmsPublisher? commandPublisher = null,
        int? publishBatchSize = null)
    {
        var guidService = MockGuidService(guidOutput);
        var dateTimeService = MockDateTimeService(dateTimeOutput);

        repository ??= new Mock<ISmsNotificationRepository>().Object;
        commandPublisher ??= new Mock<ISendSmsPublisher>().Object;

        return new SmsNotificationService(
            guidService,
            dateTimeService,
            repository,
            commandPublisher,
            Options.Create(new NotificationConfig
            {
                SmsPublishBatchSize = publishBatchSize ?? 50
            }));
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

    [Fact]
    public async Task SendNotifications_ViaWolverine_CancellationAfterFetchBeforePublish_StatusResetForBatch()
    {
        // Arrange
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "first");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990002", "second");
        var batch = new List<Sms> { firstSms, secondSms };
        
        using var cts = new CancellationTokenSource();

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .Callback<int, CancellationToken, SendingTimePolicy>((_, _, _) => cts.Cancel())
            .ReturnsAsync(batch);

        var commandPublisherMock = new Mock<ISendSmsPublisher>();
        var service = GetTestService(repository: repoMock.Object, commandPublisher: commandPublisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cts.Token));

        commandPublisherMock.Verify(p => p.PublishAsync(It.IsAny<IReadOnlyList<Sms>>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), SmsNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(batch.Count));
    }

    [Fact]
    public async Task SendNotifications_ViaWolverine_PublisherThrowsOperationCanceled_StatusResetForBatch()
    {
        // Arrange
        var firstSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "first");
        var secondSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990002", "second");
        var thirdSms = new Sms(Guid.NewGuid(), "Altinn", "+4799990003", "third");
        var batch = new List<Sms> { firstSms, secondSms, thirdSms };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch);

        var commandPublisherMock = new Mock<ISendSmsPublisher>();
        commandPublisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Sms>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var cts = new CancellationTokenSource();
        var service = GetTestService(repository: repoMock.Object, commandPublisher: commandPublisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cts.Token));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), SmsNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(batch.Count));
    }

    [Fact]
    public async Task SendNotifications_ViaWolverine_PublisherThrowsInvalidOperationException_ExceptionPropagatesAndStatusReset()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "test");
        var batch = new List<Sms> { sms };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotifications(It.IsAny<int>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(batch);

        var commandPublisherMock = new Mock<ISendSmsPublisher>();
        commandPublisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Sms>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Misconfiguration"));

        var service = GetTestService(repository: repoMock.Object, commandPublisher: commandPublisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendNotifications(CancellationToken.None));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), SmsNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(batch.Count));
    }

    [Fact]
    public async Task UpdateSendStatus_WithDeliveryReport_ForwardsDeliveryReportToRepository()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        string gatewayReference = Guid.NewGuid().ToString();
        string deliveryReport = """{"messageId":"abc","status":"Delivered","deliveryStatusDetails":{"statusMessage":"OK"}}""";

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            GatewayReference = gatewayReference,
            SendResult = SmsNotificationResultType.Delivered,
            DeliveryReport = deliveryReport
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(
            It.Is<Guid>(n => n == notificationId),
            It.Is<SmsNotificationResultType>(e => e == SmsNotificationResultType.Delivered),
            It.Is<string>(s => s.Equals(gatewayReference)),
            It.Is<string?>(d => d == deliveryReport)))
            .Returns(Task.CompletedTask);

        var service = GetTestService(repository: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify(
            r => r.UpdateSendStatus(
                It.Is<Guid>(n => n == notificationId),
                It.Is<SmsNotificationResultType>(e => e == SmsNotificationResultType.Delivered),
                It.Is<string>(s => s.Equals(gatewayReference)),
                It.Is<string?>(d => d == deliveryReport)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSendStatus_WithNullDeliveryReport_PassesNullDeliveryReportToRepository()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        string gatewayReference = Guid.NewGuid().ToString();

        SmsSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            GatewayReference = gatewayReference,
            SendResult = SmsNotificationResultType.Accepted,
            DeliveryReport = null
        };

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(
            It.Is<Guid>(n => n == notificationId),
            It.Is<SmsNotificationResultType>(e => e == SmsNotificationResultType.Accepted),
            It.Is<string>(s => s.Equals(gatewayReference)),
            It.Is<string?>(d => d == null)))
            .Returns(Task.CompletedTask);

        var service = GetTestService(repository: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify(
            r => r.UpdateSendStatus(
                It.Is<Guid>(n => n == notificationId),
                It.Is<SmsNotificationResultType>(e => e == SmsNotificationResultType.Accepted),
                It.Is<string>(s => s.Equals(gatewayReference)),
                It.Is<string?>(d => d == null)),
            Times.Once);
    }
}
