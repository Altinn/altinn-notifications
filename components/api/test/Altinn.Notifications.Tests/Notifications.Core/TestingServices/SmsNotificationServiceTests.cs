using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

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
    public async Task SendNotification_StartUnitOfWorkThrows_ReturnsFalse()
    {
        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var service = GetService(unitOfWorkRepository: unitOfWorkRepository.Object);

        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task SendNotification_NoNotification_RollsBackAndReturnsFalse()
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

        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_CancellationRequestedAfterClaim_RollsBackAndReturnsFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message", "ttd");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(sms);

        var publisher = new Mock<ISmsCommandPublisher>();

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = CreateUnitOfWork();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object);

        // Act
        var result = await service.SendNotification(cancellationToken: cts.Token);

        // Assert
        Assert.False(result);
        publisher.Verify(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_NotificationFound_PublishesAndCommits()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message", "ttd");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISmsCommandPublisher>();
        publisher
            .Setup(p => p.PublishAsync(sms, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        publisher.Verify(p => p.PublishAsync(sms, It.IsAny<CancellationToken>()), Times.Once);
        senderSubstitution.Verify(s => s.ResolveSender(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        repo.Verify(
            r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_SubstitutionConfiguredAndMatched_PersistsSubstitutedSenderWithUnitOfWork()
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

        var publisher = new Mock<ISmsCommandPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal("+4775006000", sms.Sender);
        repo.Verify(r => r.PersistSubstitutedSender(unitOfWork, sms.NotificationId, "+4775006000"), Times.Once);
    }

    [Fact]
    public async Task SendNotification_SubstitutionConfiguredButNotMatched_DoesNotPersistSubstitutedSender()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message", "digdir");

        var senderSubstitution = new Mock<ISmsSenderSubstitutionService>();
        senderSubstitution.Setup(s => s.HasRules).Returns(true);
        senderSubstitution
            .Setup(s => s.ResolveSender("Altinn", "+4799991111", "digdir"))
            .Returns(new SmsSenderResolutionResult("Altinn", false));

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISmsCommandPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        // Act
        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        repo.Verify(r => r.PersistSubstitutedSender(It.IsAny<UnitOfWork>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        publisher.Verify(p => p.PublishAsync(sms, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotification_WhenPersistSubstitutedSenderThrows_RollsBackAndReturnsFalse()
    {
        // Arrange
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
        repo
            .Setup(r => r.PersistSubstitutedSender(It.IsAny<UnitOfWork>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("persist failed"));

        var publisher = new Mock<ISmsCommandPublisher>();

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

        // Act
        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        publisher.Verify(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_PublishThrows_RollsBackAndReturnsFalse()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISmsCommandPublisher>();
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

        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        repo.Verify(
            r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_SizeLimitExceeded_SetsContentTooLargeResultInUnitOfWorkAndCommits()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "message");
        UnitOfWork unitOfWork = CreateUnitOfWork();
        using CancellationTokenSource cancellationTokenSource = new();

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISmsCommandPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationSizeLimitExceededException("too large", new InvalidOperationException()));

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var logger = new Mock<ILogger<SmsNotificationService>>();

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object,
            logger: logger.Object);

        var result = await service.SendNotification(cancellationToken: cancellationTokenSource.Token);

        Assert.True(result);
        repo.Verify(
            r => r.UpdateSendStatusAsync(unitOfWork, sms.NotificationId, SmsNotificationResultType.Failed_ContentTooLarge, null, null, cancellationTokenSource.Token),
            Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        logger.Verify(
            l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<NotificationSizeLimitExceededException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        logger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendNotification_SizeLimitExceededAndSettingResultFails_RollsBackAndReturnsFalse()
    {
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "message");
        UnitOfWork unitOfWork = CreateUnitOfWork();

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);
        repo
            .Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var publisher = new Mock<ISmsCommandPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationSizeLimitExceededException("too large", new InvalidOperationException()));

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object);

        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_CommitThrows_RollsBackAndReturnsFalse()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISmsCommandPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = CreateUnitOfWork();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);
        unitOfWorkRepository
            .Setup(r => r.CommitUnitOfWork(unitOfWork))
            .ThrowsAsync(new InvalidOperationException("commit failed"));

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object);

        // Act
        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
    }

    [Fact]
    public async Task SendNotification_PublishThrowsAndRollbackThrows_ReturnsFalse()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799991111", "message");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.GetNewNotification(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ReturnsAsync(sms);

        var publisher = new Mock<ISmsCommandPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<Sms>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("publish failed"));

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = CreateUnitOfWork();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);
        unitOfWorkRepository
            .Setup(r => r.RollbackUnitOfWork(unitOfWork))
            .ThrowsAsync(new InvalidOperationException("rollback failed"));

        var service = GetService(
            repository: repo.Object,
            commandPublisher: publisher.Object,
            unitOfWorkRepository: unitOfWorkRepository.Object);

        // Act
        var result = await service.SendNotification(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TerminateExpiredNotifications_ForwardsToRepository()
    {
        // Arrange
        var repo = new Mock<ISmsNotificationRepository>();
        var service = GetService(repository: repo.Object);

        // Act
        await service.TerminateExpiredNotifications();

        // Assert
        repo.Verify(r => r.TerminateExpiredNotifications(), Times.Once);
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
            r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), sendResult.NotificationId, sendResult.SendResult, sendResult.GatewayReference, sendResult.DeliveryReport),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSendStatus_RepositorySucceeds_CommitsUnitOfWork()
    {
        UnitOfWork unitOfWork = CreateUnitOfWork();
        var repo = new Mock<ISmsNotificationRepository>();

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(repository: repo.Object, unitOfWorkRepository: unitOfWorkRepository.Object);

        await service.UpdateSendStatus(new SmsSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = SmsNotificationResultType.Delivered });

        repo.Verify(
            r => r.UpdateSendStatusAsync(unitOfWork, It.IsAny<Guid?>(), SmsNotificationResultType.Delivered, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_RepositoryThrows_RollsBackAndRethrows()
    {
        UnitOfWork unitOfWork = CreateUnitOfWork();
        var notFound = new NotificationNotFoundException(NotificationChannel.Sms, Guid.NewGuid().ToString(), SendStatusIdentifierType.NotificationId);

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound);

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);

        var service = GetService(repository: repo.Object, unitOfWorkRepository: unitOfWorkRepository.Object);

        var exception = await Assert.ThrowsAsync<NotificationNotFoundException>(
            () => service.UpdateSendStatus(new SmsSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = SmsNotificationResultType.Delivered }));

        Assert.Same(notFound, exception);
        unitOfWorkRepository.Verify(r => r.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_RollbackFails_LogsErrorAndRethrowsOriginalException()
    {
        UnitOfWork unitOfWork = CreateUnitOfWork();
        var updateFailure = new InvalidOperationException("update failed");
        var rollbackFailure = new InvalidOperationException("rollback failed");

        var repo = new Mock<ISmsNotificationRepository>();
        repo
            .Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<SmsNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(updateFailure);

        var unitOfWorkRepository = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepository
            .Setup(r => r.StartUnitOfWork())
            .ReturnsAsync(unitOfWork);
        unitOfWorkRepository
            .Setup(r => r.RollbackUnitOfWork(unitOfWork))
            .ThrowsAsync(rollbackFailure);

        var logger = new Mock<ILogger<SmsNotificationService>>();

        var service = GetService(repository: repo.Object, unitOfWorkRepository: unitOfWorkRepository.Object, logger: logger.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateSendStatus(new SmsSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = SmsNotificationResultType.Delivered }));

        Assert.Same(updateFailure, exception);
        unitOfWorkRepository.Verify(r => r.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        logger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.Is<Exception?>(e => ReferenceEquals(e, rollbackFailure)), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
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
        ISmsCommandPublisher? commandPublisher = null,
        IUnitOfWorkRepository? unitOfWorkRepository = null,
        ISmsSenderSubstitutionService? senderSubstitutionService = null,
        ILogger<SmsNotificationService>? logger = null)
    {
        var guidService = new Mock<IGuidService>();
        guidService.Setup(g => g.NewGuid()).Returns(guidOutput ?? Guid.NewGuid());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService.Setup(d => d.UtcNow()).Returns(dateTimeOutput ?? DateTime.UtcNow);

        repository ??= new Mock<ISmsNotificationRepository>().Object;
        commandPublisher ??= new Mock<ISmsCommandPublisher>().Object;
        unitOfWorkRepository ??= new Mock<IUnitOfWorkRepository>().Object;
        senderSubstitutionService ??= new Mock<ISmsSenderSubstitutionService>().Object;

        return new SmsNotificationService(
            guidService.Object,
            dateTimeService.Object,
            repository,
            commandPublisher,
            unitOfWorkRepository,
            logger ?? new Mock<ILogger<SmsNotificationService>>().Object,
            senderSubstitutionService);
    }
}
