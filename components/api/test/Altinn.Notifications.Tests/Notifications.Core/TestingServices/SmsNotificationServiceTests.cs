using System;
using System.Collections.Generic;
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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class SmsNotificationServiceTests
{
    [Fact]
    public async Task CreateNotification_RecipientNumberIsDefined_ResultNew()
    {
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;

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

        var service = GetTestService(guidOutput: id, dateTimeOutput: dateTimeOutput);

        var result = await service.CreateNotification(orderId, requestedSendTime, requestedSendTime.AddHours(48), [new("+4799999999")], new SmsRecipient());

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equivalent(expected, result[0]);
    }

    [Fact]
    public async Task CreateNotification_RecipientIsReserved_IgnoreReservationsFalse_ResultFailedRecipientReserved()
    {
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;

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

        var service = GetTestService(guidOutput: id, dateTimeOutput: dateTimeOutput);

        var result = await service.CreateNotification(orderId, requestedSendTime, requestedSendTime.AddHours(48), [], new SmsRecipient { IsReserved = true });

        Assert.Single(result);
        Assert.Equivalent(expected, result[0]);
    }

    [Fact]
    public async Task SendNotifications_NoNotification_ReturnsFalse_AndRollsBack()
    {
        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync((Sms?)null);

        var unitOfWorkRepoMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepoMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepoMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(repository: repoMock.Object, unitOfWorkRepository: unitOfWorkRepoMock.Object);

        bool sent = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.False(sent);
        unitOfWorkRepoMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepoMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_FoundNotification_ReturnsTrue_AndCommits()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "test");

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisherMock = new Mock<ISendSmsPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sms?)null);

        var unitOfWorkRepoMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepoMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepoMock.Setup(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(repository: repoMock.Object, commandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepoMock.Object);

        bool sent = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.True(sent);
        publisherMock.Verify(p => p.PublishAsync(It.Is<Sms>(m => m.NotificationId == sms.NotificationId), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkRepoMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_CancellationBeforePublish_ReturnsFalse_AndDoesNotPublish()
    {
        using var cts = new CancellationTokenSource();
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "test");

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(sms);

        var publisherMock = new Mock<ISendSmsPublisher>();

        var service = GetTestService(repository: repoMock.Object, commandPublisher: publisherMock.Object);

        bool sent = await service.SendNotifications(cts.Token);

        Assert.False(sent);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_PublisherFailure_ReturnsFalse_AndRollsBack()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799990001", "test");

        var repoMock = new Mock<ISmsNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisherMock = new Mock<ISendSmsPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Misconfiguration"));

        var unitOfWorkRepoMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepoMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepoMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(repository: repoMock.Object, commandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepoMock.Object);

        bool sent = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.False(sent);
        unitOfWorkRepoMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepoMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_WithDeliveryReport_ForwardsDeliveryReportToRepository()
    {
        Guid notificationId = Guid.NewGuid();
        string gatewayReference = Guid.NewGuid().ToString();
        string deliveryReport = """{"messageId":"abc","status":"Delivered"}""";

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

        await service.UpdateSendStatus(sendOperationResult);

        repoMock.Verify(
            r => r.UpdateSendStatus(
                It.Is<Guid>(n => n == notificationId),
                It.Is<SmsNotificationResultType>(e => e == SmsNotificationResultType.Delivered),
                It.Is<string>(s => s.Equals(gatewayReference)),
                It.Is<string?>(d => d == deliveryReport)),
            Times.Once);
    }

    private static SmsNotificationService GetTestService(
        Guid? guidOutput = null,
        DateTime? dateTimeOutput = null,
        ISmsNotificationRepository? repository = null,
        ISendSmsPublisher? commandPublisher = null,
        IUnitOfWorkRepository? unitOfWorkRepository = null)
    {
        var guidService = new Mock<IGuidService>();
        guidService.Setup(g => g.NewGuid()).Returns(guidOutput ?? Guid.NewGuid());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService.Setup(d => d.UtcNow()).Returns(dateTimeOutput ?? DateTime.UtcNow);

        repository ??= new Mock<ISmsNotificationRepository>().Object;
        commandPublisher ??= new Mock<ISendSmsPublisher>().Object;

        if (unitOfWorkRepository is null)
        {
            var unitOfWorkRepoMock = new Mock<IUnitOfWorkRepository>();
            unitOfWorkRepoMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
            unitOfWorkRepoMock.Setup(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);
            unitOfWorkRepoMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);
            unitOfWorkRepository = unitOfWorkRepoMock.Object;
        }

        return new SmsNotificationService(
            guidService.Object,
            dateTimeService.Object,
            repository,
            commandPublisher,
            Options.Create(new NotificationConfig { SmsPublishBatchSize = 50 }),
            unitOfWorkRepository,
            new Mock<ILogger<SmsNotificationService>>().Object);
    }
}
