using Altinn.Notifications.Core.Configuration;
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
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class EmailNotificationServiceTests
{
    private readonly Email _email = new(Guid.NewGuid(), "email.subject", "email.body", "from@domain.com", "to@domain.com", EmailContentType.Plain);
    private readonly ComposedEmail _composedEmail = new(Guid.NewGuid(), "composed.subject", "composed.body", "from@domain.com", "to@domain.com", EmailContentType.Plain, []);

    [Fact]
    public async Task CreateNotification_ToAddressDefined_ResultNew()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        var emailRecipient = new EmailRecipient() { OrganizationNumber = "skd-orgno" };
        var emailAddressPoints = new List<EmailAddressPoint>() { new("skd@norge.no") };

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

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        var result = await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient);

        // Assert
        var singleResult = Assert.Single(result);
        Assert.Equivalent(expected, singleResult);
    }

    [Fact]
    public async Task CreateNotification_RecipientIsReserved_IgnoreReservationsFalse_ResultFailedRecipientReserved()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        var emailRecipient = new EmailRecipient() { IsReserved = true };
        var emailAddressPoints = new List<EmailAddressPoint>() { new("skd@norge.no") };

        EmailNotification expected = new()
        {
            Id = id,
            OrderId = orderId,
            Recipient = new()
            {
                IsReserved = true,
                ToAddress = string.Empty
            },
            RequestedSendTime = requestedSendTime,
            SendResult = new(EmailNotificationResultType.Failed_RecipientReserved, dateTimeOutput)
        };

        var repoMock = new Mock<IEmailNotificationRepository>();

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        var result = await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient);

        // Assert
        var singleResult = Assert.Single(result);
        Assert.Equivalent(expected, singleResult);
    }

    [Fact]
    public async Task CreateNotification_RecipientIsReserved_IgnoreReservationsTrue_ResultNew()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        var emailRecipient = new EmailRecipient() { IsReserved = true };
        var emailAddressPoints = new List<EmailAddressPoint>() { new("email@domain.com") };

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

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        var result = await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient, true);

        // Assert
        var singleResult = Assert.Single(result);
        Assert.Equivalent(expected, singleResult);
    }

    [Fact]
    public async Task CreateNotification_ToAddressMissing_LookupFails_ResultFailedRecipientNotDefined()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        var emailAddressPoints = new List<EmailAddressPoint>();
        var emailRecipient = new EmailRecipient() { OrganizationNumber = "skd-orgno" };

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

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        var result = await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient);

        // Assert
        var singleResult = Assert.Single(result);
        Assert.Equivalent(expected, singleResult);
    }

    [Fact]
    public async Task CreateNotification_RecipientHasTwoEmailAddresses_ResultHasOneItemForEachAddress()
    {
        // Arrange
        var expectedEmailAddress1 = "user_1@domain.com";
        var expectedEmailAddress2 = "user_2@domain.com";

        var emailRecipient = new EmailRecipient() { OrganizationNumber = "org" };
        var emailAddressPoints = new List<EmailAddressPoint>() { new(expectedEmailAddress1), new(expectedEmailAddress2) };

        var repoMock = new Mock<IEmailNotificationRepository>();
        var service = GetTestService(repo: repoMock.Object);

        // Act
        var result = await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, emailAddressPoints, emailRecipient);

        // Assert
        Assert.Equal(2, result.Count(x => x.Recipient.OrganizationNumber == "org"));
        Assert.Equal(expectedEmailAddress1, result[0].Recipient.ToAddress);
        Assert.Equal(expectedEmailAddress2, result[1].Recipient.ToAddress);
    }

    [Fact]
    public async Task UpdateSendStatus_SendResultDefined_Succeeded()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Succeeded
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.Is<Guid>(n => n == notificationId), It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded), It.Is<string>(s => s.Equals(operationId)))).Verifiable();

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
        Guid notificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Failed_TransientError
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatusAsync(
            It.IsAny<UnitOfWork>(),
            It.Is<Guid>(n => n == notificationId),
            It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.New),
            It.Is<string>(s => s.Equals(operationId))))
            .Verifiable();

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify();
    }

    [Fact]
    public async Task UpdateStatus_WhenStatusIsSucceeded_ShouldPassStatusIsAcceptedOrSucceededAsTrue()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();
        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Succeeded
        };

        var mockRepo = new Mock<EmailNotificationRepository>(null!, null!, Options.Create(new NotificationConfig()))
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

        var service = new EmailNotificationService(
            new Mock<IGuidService>().Object,
            new Mock<IDateTimeService>().Object,
            new Mock<IEmailCommandPublisher>().Object,
            mockRepo.Object,
            new Mock<IComposedEmailCommandPublisher>().Object,
            new Mock<IUnitOfWorkRepository>().Object,
            new Mock<ILogger<EmailNotificationService>>().Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert - verify ExecuteUpdateWithTransactionAsync was called with statusIsAcceptedOrSucceeded = true
        mockRepo.Protected()
            .Verify<Task>(
                "ExecuteUpdateWithTransactionAsync",
                Times.Once(),
                ItExpr.IsAny<string>(),
                ItExpr.IsAny<Action<NpgsqlCommand>>(),
                ItExpr.Is<NotificationChannel>(c => c == NotificationChannel.Email),
                ItExpr.IsAny<Guid?>(),
                ItExpr.IsAny<string?>(),
                ItExpr.Is<bool>(b => b),
                ItExpr.IsAny<SendStatusIdentifierType>());
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(SendingTimePolicy.Daytime)]
    public async Task SendNotification_PassesSendingTimePolicyToRepository(SendingTimePolicy sendingTimePolicy)
    {
        // Arrange
        UnitOfWork unitOfWork = new();
        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .Setup(r => r.GetNewNotificationAsync(unitOfWork, It.IsAny<CancellationToken>(), sendingTimePolicy))
            .ReturnsAsync((Email?)null);

        var service = GetTestService(repo: repoMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(sendingTimePolicy, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        repoMock.Verify(r => r.GetNewNotificationAsync(unitOfWork, It.IsAny<CancellationToken>(), sendingTimePolicy), Times.Once);
        repoMock.Verify(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>(), It.Is<SendingTimePolicy>(p => p != sendingTimePolicy)), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenRepositoryThrowsOperationCanceledDuringFetch_RollsBackAndReturnsFalse()
    {
        // Arrange
        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .Setup(e => e.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException()); // Simulate cancellation during fetch

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenStartUnitOfWorkThrows_ReturnsFalseAndDoesNotPublish()
    {
        // Arrange
        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock
            .Setup(u => u.StartUnitOfWork())
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        var repoMock = new Mock<IEmailNotificationRepository>();
        var publisherMock = new Mock<IEmailCommandPublisher>();

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        repoMock.Verify(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenNoNotificationClaimed_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Email?)null);

        var publisherMock = new Mock<IEmailCommandPublisher>();

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenPublishSucceeds_UsesCommitNotRollback()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepositoryMock.Setup(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenSizeLimitExceeded_SetsContentTooLargeResultInUnitOfWorkAndCommits()
    {
        // Arrange
        var unitOfWork = new UnitOfWork();
        using CancellationTokenSource cancellationTokenSource = new();

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationSizeLimitExceededException("too large", new InvalidOperationException()));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var loggerMock = new Mock<ILogger<EmailNotificationService>>();

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object, logger: loggerMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(wasSent);
        repoMock.Verify(
            r => r.UpdateSendStatusAsync(unitOfWork, _email.NotificationId, EmailNotificationResultType.Failed_ContentTooLarge, null, null, null, cancellationTokenSource.Token),
            Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        VerifySizeLimitLoggedAsWarning(loggerMock);
    }

    [Fact]
    public async Task SendNotification_WhenSizeLimitExceededAndSettingResultFails_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);
        repoMock.Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationSizeLimitExceededException("too large", new InvalidOperationException()));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenCommitThrows_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepositoryMock.Setup(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()))
            .ThrowsAsync(new InvalidOperationException("commit failed"));
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
    }

    [Fact]
    public async Task SendNotification_WhenPublisherThrowsAndRollbackThrows_ReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("publish failed"));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()))
            .ThrowsAsync(new InvalidOperationException("rollback failed"));

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
    }

    [Fact]
    public async Task SendNotification_WhenCancellationRequestedAfterClaim_RollsBackAndDoesNotPublish()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, cts.Token);

        // Assert
        Assert.False(wasSent);

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenPublisherThrowsOperationCanceled_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenPublisherThrowsInvalidOperation_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Misconfiguration"));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenPublisherThrowsUnexpectedException_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Unexpected timeout"));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            emailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendNotification(SendingTimePolicy.Anytime, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_RepositoryThrowsOperationCanceledDuringFetch_NoStatusResets()
    {
        // Arrange
        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .Setup(e => e.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenStartUnitOfWorkThrows_ReturnsFalseAndDoesNotPublish()
    {
        // Arrange
        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock
            .Setup(u => u.StartUnitOfWork())
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        var repoMock = new Mock<IEmailNotificationRepository>();
        var publisherMock = new Mock<IComposedEmailCommandPublisher>();

        var service = GetTestService(
            repo: repoMock.Object,
            composedEmailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        repoMock.Verify(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenNoNotificationClaimed_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComposedEmail?)null);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            composedEmailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenCancellationRequestedAfterClaim_RollsBackAndDoesNotPublish()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            composedEmailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(cts.Token);

        // Assert
        Assert.False(wasSent);

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenPublishSucceeds_UsesCommitNotRollback()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepositoryMock.Setup(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(wasSent);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenSizeLimitExceeded_SetsContentTooLargeResultInUnitOfWorkAndCommits()
    {
        // Arrange
        var unitOfWork = new UnitOfWork();
        using CancellationTokenSource cancellationTokenSource = new();

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationSizeLimitExceededException("too large", new InvalidOperationException()));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var loggerMock = new Mock<ILogger<EmailNotificationService>>();

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object, logger: loggerMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(cancellationTokenSource.Token);

        // Assert
        Assert.True(wasSent);
        repoMock.Verify(
            r => r.UpdateSendStatusAsync(unitOfWork, _composedEmail.NotificationId, EmailNotificationResultType.Failed_ContentTooLarge, null, null, null, cancellationTokenSource.Token),
            Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        VerifySizeLimitLoggedAsWarning(loggerMock);
    }

    [Fact]
    public async Task SendComposedNotification_WhenSizeLimitExceededAndSettingResultFails_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);
        repoMock.Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotificationSizeLimitExceededException("too large", new InvalidOperationException()));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenCommitThrows_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(new UnitOfWork());
        unitOfWorkRepositoryMock.Setup(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()))
            .ThrowsAsync(new InvalidOperationException("commit failed"));
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>())).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            composedEmailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Once);
    }

    [Fact]
    public async Task SendComposedNotification_WhenPublisherThrowsOperationCanceled_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            composedEmailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenPublisherThrowsInvalidOperation_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Misconfiguration"));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            composedEmailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_WhenPublisherThrowsUnexpectedException_RollsBackAndReturnsFalse()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<UnitOfWork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Unexpected timeout"));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        UnitOfWork unitOfWork = new();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).Returns(Task.CompletedTask);

        var service = GetTestService(
            repo: repoMock.Object,
            composedEmailCommandPublisher: publisherMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_WithDeliveryReport_ForwardsDeliveryReportToRepository()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();
        string deliveryReport = """{"messageId":"abc","status":"Delivered","deliveryStatusDetails":{"statusMessage":"OK"}}""";

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Succeeded,
            DeliveryReport = deliveryReport
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatusAsync(
            It.IsAny<UnitOfWork>(),
            It.Is<Guid>(n => n == notificationId),
            It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded),
            It.Is<string>(s => s.Equals(operationId)),
            It.Is<string?>(d => d == deliveryReport)))
            .Returns(Task.CompletedTask);

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify(
            r => r.UpdateSendStatusAsync(
                It.IsAny<UnitOfWork>(),
                It.Is<Guid>(n => n == notificationId),
                It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded),
                It.Is<string>(s => s.Equals(operationId)),
                It.Is<string?>(d => d == deliveryReport)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSendStatus_WithNullDeliveryReport_PassesNullDeliveryReportToRepository()
    {
        // Arrange
        Guid notificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Succeeded,
            DeliveryReport = null
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatusAsync(
            It.IsAny<UnitOfWork>(),
            It.Is<Guid>(n => n == notificationId),
            It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded),
            It.Is<string>(s => s.Equals(operationId)),
            It.Is<string?>(d => d == null)))
            .Returns(Task.CompletedTask);

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify(
            r => r.UpdateSendStatusAsync(
                It.IsAny<UnitOfWork>(),
                It.Is<Guid>(n => n == notificationId),
                It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded),
                It.Is<string>(s => s.Equals(operationId)),
                It.Is<string?>(d => d == null)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSendStatus_TransientErrorWithDeliveryReport_ConvertedToNewAndDeliveryReportForwarded()
    {
        // Arrange — a transient error still carries a delivery report payload; it should be forwarded
        // even after the result is reset to New for re-processing.
        Guid notificationId = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();
        string deliveryReport = """{"messageId":"abc","status":"TransientFailure"}""";

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationId,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Failed_TransientError,
            DeliveryReport = deliveryReport
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatusAsync(
            It.IsAny<UnitOfWork>(),
            It.Is<Guid>(n => n == notificationId),
            It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.New),
            It.Is<string>(s => s.Equals(operationId)),
            It.Is<string?>(d => d == deliveryReport)))
            .Returns(Task.CompletedTask);

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert — result was mapped to New, but delivery report is still forwarded
        repoMock.Verify(
            r => r.UpdateSendStatusAsync(
                It.IsAny<UnitOfWork>(),
                It.Is<Guid>(n => n == notificationId),
                It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.New),
                It.Is<string>(s => s.Equals(operationId)),
                It.Is<string?>(d => d == deliveryReport)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSendStatus_RepositorySucceeds_CommitsUnitOfWork()
    {
        // Arrange
        var unitOfWork = new UnitOfWork();
        var repoMock = new Mock<IEmailNotificationRepository>();

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var service = GetTestService(repo: repoMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        await service.UpdateSendStatus(new EmailSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = EmailNotificationResultType.Delivered });

        // Assert
        repoMock.Verify(
            r => r.UpdateSendStatusAsync(unitOfWork, It.IsAny<Guid?>(), EmailNotificationResultType.Delivered, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_RepositoryThrows_RollsBackAndRethrows()
    {
        // Arrange
        var unitOfWork = new UnitOfWork();
        var notFound = new NotificationNotFoundException(NotificationChannel.Email, Guid.NewGuid().ToString(), SendStatusIdentifierType.NotificationId);

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var service = GetTestService(repo: repoMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        // Act
        var exception = await Assert.ThrowsAsync<NotificationNotFoundException>(
            () => service.UpdateSendStatus(new EmailSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = EmailNotificationResultType.Delivered }));

        // Assert
        Assert.Same(notFound, exception);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSendStatus_RollbackFails_LogsErrorAndRethrowsOriginalException()
    {
        // Arrange
        var unitOfWork = new UnitOfWork();
        var updateFailure = new InvalidOperationException("update failed");
        var rollbackFailure = new InvalidOperationException("rollback failed");

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatusAsync(It.IsAny<UnitOfWork>(), It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(updateFailure);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);
        unitOfWorkRepositoryMock.Setup(u => u.RollbackUnitOfWork(unitOfWork)).ThrowsAsync(rollbackFailure);

        var loggerMock = new Mock<ILogger<EmailNotificationService>>();

        var service = GetTestService(repo: repoMock.Object, unitOfWorkRepository: unitOfWorkRepositoryMock.Object, logger: loggerMock.Object);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateSendStatus(new EmailSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = EmailNotificationResultType.Delivered }));

        // Assert
        Assert.Same(updateFailure, exception);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        loggerMock.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.Is<Exception?>(e => ReferenceEquals(e, rollbackFailure)), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static void VerifySizeLimitLoggedAsWarning(Mock<ILogger<EmailNotificationService>> loggerMock)
    {
        loggerMock.Verify(
            l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<NotificationSizeLimitExceededException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        loggerMock.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private static EmailNotificationService GetTestService(IEmailNotificationRepository? repo = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null, IEmailCommandPublisher? emailCommandPublisher = null, IComposedEmailCommandPublisher? composedEmailCommandPublisher = null, IUnitOfWorkRepository? unitOfWorkRepository = null, ILogger<EmailNotificationService>? logger = null)
    {
        var guidService = new Mock<IGuidService>();
        guidService
            .Setup(g => g.NewGuid())
            .Returns(guidOutput ?? Guid.NewGuid());

        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService
            .Setup(d => d.UtcNow())
            .Returns(dateTimeOutput ?? DateTime.UtcNow);

        repo ??= new Mock<IEmailNotificationRepository>().Object;
        emailCommandPublisher ??= new Mock<IEmailCommandPublisher>().Object;
        composedEmailCommandPublisher ??= new Mock<IComposedEmailCommandPublisher>().Object;
        if (unitOfWorkRepository is null)
        {
            var unitOfWorkRepoMock = new Mock<IUnitOfWorkRepository>();
            unitOfWorkRepoMock
                .Setup(u => u.StartUnitOfWork())
                .ReturnsAsync(new UnitOfWork());
            unitOfWorkRepoMock
                .Setup(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()))
                .Returns(Task.CompletedTask);
            unitOfWorkRepoMock
                .Setup(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()))
                .Returns(Task.CompletedTask);
            unitOfWorkRepository = unitOfWorkRepoMock.Object;
        }

        return new EmailNotificationService(
            guidService.Object,
            dateTimeService.Object,
            emailCommandPublisher,
            repo,
            composedEmailCommandPublisher,
            unitOfWorkRepository,
            logger ?? new Mock<ILogger<EmailNotificationService>>().Object);
    }
}
