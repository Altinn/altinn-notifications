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
    public async Task CreateNotification_ReservedRecipientWithoutIgnore_ReturnsFailedRecipientReserved()
    {
        Guid expectedId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        var service = GetService(guidOutput: expectedId, dateTimeOutput: now);

        var result = await service.CreateNotification(
            Guid.NewGuid(),
            now,
            now.AddDays(1),
            [],
            new SmsRecipient { IsReserved = true },
            ignoreReservation: false);

        SmsNotification notification = Assert.Single(result);
        Assert.Equal(expectedId, notification.Id);
        Assert.Equal(SmsNotificationResultType.Failed_RecipientReserved, notification.SendResult.Result);
        Assert.Equal(string.Empty, notification.Recipient.MobileNumber);
    }

    [Fact]
    public async Task CreateNotification_NoAddressPoints_ReturnsFailedRecipientNotIdentified()
    {
        Guid expectedId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        var service = GetService(guidOutput: expectedId, dateTimeOutput: now);

        var result = await service.CreateNotification(
            Guid.NewGuid(),
            now,
            now.AddDays(1),
            [],
            new SmsRecipient { MobileNumber = "+4799999999" },
            ignoreReservation: true);

        SmsNotification notification = Assert.Single(result);
        Assert.Equal(SmsNotificationResultType.Failed_RecipientNotIdentified, notification.SendResult.Result);
    }

    [Fact]
    public async Task CreateNotification_AddressPointsPresent_ReturnsOneNotificationPerAddressPoint()
    {
        DateTime now = DateTime.UtcNow;
        var service = GetService(dateTimeOutput: now);

        var result = await service.CreateNotification(
            Guid.NewGuid(),
            now,
            now.AddDays(1),
            [new SmsAddressPoint("+4711111111"), new SmsAddressPoint("+4722222222")],
            new SmsRecipient { OrganizationNumber = "991825827" },
            ignoreReservation: true);

        Assert.Equal(2, result.Count);
        Assert.All(result, n => Assert.Equal(SmsNotificationResultType.New, n.SendResult.Result));
        Assert.Contains(result, n => n.Recipient.MobileNumber == "+4711111111");
        Assert.Contains(result, n => n.Recipient.MobileNumber == "+4722222222");
    }

    [Fact]
    public async Task SendNotifications_StartUnitOfWorkThrows_ReturnsFalse()
    {
        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var service = GetService(unitOfWorkRepository: unitOfWorkRepository.Object);

        var result = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task SendNotifications_NoNotification_RollsBackAndReturnsFalse()
    {
        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync((Sms?)null);

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = CreateUnitOfWork();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(repository: repo.Object, unitOfWorkRepository: unitOfWorkRepository.Object);

        var result = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_NotificationFound_PublishesAndCommits()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message", "ttd");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISendSmsPublisher>();
        publisher
            .Setup(p => p.PublishAsync(sms, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sms?)null);

        var senderSubstitution = new Mock<ISmsSenderSubstitutionService>();
        senderSubstitution.Setup(s => s.HasRules).Returns(false);

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = CreateUnitOfWork();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object,
            senderSubstitutionService: senderSubstitution.Object);

        var result = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.True(result);
        publisher.Verify(p => p.PublishAsync(sms, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_SubstitutionConfiguredAndMatched_PersistsSubstitutedSenderWithUnitOfWork()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+34123456789", "message", "digdir");

        var senderSubstitution = new Mock<ISmsSenderSubstitutionService>();
        senderSubstitution.Setup(s => s.HasRules).Returns(true);
        senderSubstitution
            .Setup(s => s.ResolveSender("Altinn", "+34123456789", "digdir"))
            .Returns(new SmsSenderResolutionResult("+4775006000", true));

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISendSmsPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sms?)null);

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = CreateUnitOfWork();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object,
            senderSubstitutionService: senderSubstitution.Object);

        var result = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal("+4775006000", sms.Sender);
        repo.Verify(r => r.PersistSubstitutedSender(unitOfWork, sms.NotificationId, "+4775006000"), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_PublishThrows_RollsBackAndReturnsFalse()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISendSmsPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("publish failed"));

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = CreateUnitOfWork();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object);

        var result = await service.SendNotifications(TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_ForwardsAllDataToRepository()
    {
        var sendResult = new SmsSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            SendResult = SmsNotificationResultType.Accepted,
            GatewayReference = "gw-ref",
            DeliveryReport = "{\"status\":\"delivered\"}"
        };

        var repo = new Mock<ISmsNotificationRepository>();
        var service = GetService(repository: repo.Object);

        await service.UpdateSendStatus(sendResult);

        repo.Verify(
            r => r.UpdateSendStatus(sendResult.NotificationId, sendResult.SendResult, sendResult.GatewayReference, sendResult.DeliveryReport),
            Times.Once);
    }

    private static UnitOfWork CreateUnitOfWork()
    {
        return new UnitOfWork();
    }

    private static SmsNotificationService GetService(
        Guid? guidOutput = null,
        DateTime? dateTimeOutput = null,
        ISmsNotificationRepository? repository = null,
        ISendSmsPublisher? commandPublisher = null,
        IUnitOfWorkRepository? unitOfWorkRepository = null,
        ISmsSenderSubstitutionService? senderSubstitutionService = null)
    {
        var guidService = new Mock<IGuidService>();
        guidService.Setup(g => g.NewGuid()).Returns(guidOutput ?? Guid.NewGuid());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService.Setup(d => d.UtcNow()).Returns(dateTimeOutput ?? DateTime.UtcNow);

        repository ??= new Mock<ISmsNotificationRepository>().Object;
        commandPublisher ??= new Mock<ISendSmsPublisher>().Object;
        unitOfWorkRepository ??= new Mock<IUnitOfWorkRepository>().Object;
        senderSubstitutionService ??= new Mock<ISmsSenderSubstitutionService>().Object;

        return new SmsNotificationService(
            guidService.Object,
            dateTimeService.Object,
            repository,
            commandPublisher,
            Options.Create(new NotificationConfig()),
            unitOfWorkRepository,
            new Mock<ILogger<SmsNotificationService>>().Object,
            senderSubstitutionService);
    }
}
