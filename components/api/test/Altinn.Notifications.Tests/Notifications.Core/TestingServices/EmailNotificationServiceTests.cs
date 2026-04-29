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
    private readonly int _publishBatchSize = 500;
    private readonly Email _email = new(Guid.NewGuid(), "email.subject", "email.body", "from@domain.com", "to@domain.com", EmailContentType.Plain);

    [Fact]
    public async Task CreateNotification_ToAddressDefined_ResultNew()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        DateTime requestedSendTime = DateTime.UtcNow;
        DateTime dateTimeOutput = DateTime.UtcNow;
        DateTime expectedExpiry = requestedSendTime.AddHours(48);
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
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, expectedExpiry, emailAddressPoints, emailRecipient);

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
        DateTime expectedExpiry = requestedSendTime.AddHours(48);
        var emailRecipient = new EmailRecipient() { IsReserved = true };
        var emailAddressPoints = new List<EmailAddressPoint>() { new("skd@norge.no") };

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
        await service.CreateNotification(orderId, requestedSendTime, expectedExpiry, emailAddressPoints, emailRecipient);

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
        DateTime expectedExpiry = requestedSendTime.AddHours(48);
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
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act
        await service.CreateNotification(orderId, requestedSendTime, expectedExpiry, emailAddressPoints, emailRecipient, true);

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
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(e => AssertUtils.AreEquivalent(expected, e)), It.Is<DateTime>(d => d == expectedExpiry)));

        var service = GetTestService(repo: repoMock.Object, guidOutput: id, dateTimeOutput: dateTimeOutput);

        // Act — caller-supplied expiry is irrelevant here because the not-identified branch overrides it with UtcNow.
        await service.CreateNotification(orderId, requestedSendTime, requestedSendTime.AddHours(48), emailAddressPoints, emailRecipient);

        // Assert
        repoMock.Verify();
    }

    [Fact]
    public async Task CreateNotification_RecipientHasTwoEmailAddresses_RepositoryCalledOnceForEachAddress()
    {
        // Arrange
        var emailRecipient = new EmailRecipient() { OrganizationNumber = "org" };
        var emailAddressPoints = new List<EmailAddressPoint>() { new("user_1@domain.com"), new("user_2@domain.com") };
        var requestedSendTime = DateTime.UtcNow;

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>()));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.CreateNotification(Guid.NewGuid(), requestedSendTime, requestedSendTime.AddHours(48), emailAddressPoints, emailRecipient);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<EmailNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>()), Times.Exactly(2));
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
        repoMock.Setup(r => r.UpdateSendStatus(It.Is<Guid>(n => n == notificationId), It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded), It.Is<string>(s => s.Equals(operationId))));

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
        repoMock.Setup(r => r.UpdateSendStatus(
            It.Is<Guid>(n => n == notificationId),
            It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.New),
            It.Is<string>(s => s.Equals(operationId))));

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

        var mockRepo = new Mock<EmailNotificationRepository>(
            (null as NpgsqlDataSource)!,
            (null as ILogger<EmailNotificationRepository>)!,
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

        var service = new EmailNotificationService(
            new Mock<IGuidService>().Object,
            new Mock<IDateTimeService>().Object,
            new Mock<IEmailCommandPublisher>().Object,
            Options.Create(new NotificationConfig { EmailPublishBatchSize = _publishBatchSize }),
            mockRepo.Object);

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

    [Fact]
    public async Task SendNotifications_CancellationRequested_StopsProcessing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync([_email]);

        var service = GetTestService(repo: repoMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await service.SendNotifications(cts.Token));
    }

    [Fact]
    public async Task SendNotifications_RepositoryThrowsOperationCanceledDuringFetch_NoStatusResets()
    {
        // Arrange
        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .Setup(e => e.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException()); // Simulate cancellation during fetch

        using var cancellationTokenSource = new CancellationTokenSource();

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cancellationTokenSource.Token));

        emailNotificationRepositoryMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_CancellationAfterPublishBeforeNextFetch_NoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .SetupSequence(r => r.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_email, _email]) // First fetch returns a batch to process
            .ThrowsAsync(new OperationCanceledException()); // Second fetch simulates cancellation between iterations

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);
        using var cts = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cts.Token));

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_AllEmailsPublishedSuccessfully_NoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_email, _email])
            .ReturnsAsync([]);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_PublisherFailsForAllEmails_AllEmailsResetToNew()
    {
        // Arrange
        Email firstEmail = new(Guid.NewGuid(), "first.email.subject", "first.email.body", "from-first@domain.com", "to-first@domain.com", EmailContentType.Plain);
        Email secondEmail = new(Guid.NewGuid(), "second.email.subject", "second.email.body", "from-second@domain.com", "to-second@domain.com", EmailContentType.Plain);
        Email thirdEmail = new(Guid.NewGuid(), "third.email.subject", "third.email.body", "from-third@domain.com", "to-third@domain.com", EmailContentType.Plain);

        var batch = new List<Email> { firstEmail, secondEmail, thirdEmail };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstEmail, secondEmail, thirdEmail]);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        repoMock.Verify(r => r.UpdateSendStatus(firstEmail.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(secondEmail.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(thirdEmail.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_PublisherFailsForSubset_OnlyFailedEmailsResetToNew()
    {
        // Arrange
        Email firstEmail = new(Guid.NewGuid(), "first.email.subject", "first.email.body", "from-first@domain.com", "to-first@domain.com", EmailContentType.Plain);
        Email secondEmail = new(Guid.NewGuid(), "second.email.subject", "second.email.body", "from-second@domain.com", "to-second@domain.com", EmailContentType.Plain);
        Email thirdEmail = new(Guid.NewGuid(), "third.email.subject", "third.email.body", "from-third@domain.com", "to-third@domain.com", EmailContentType.Plain);
        var batch = new List<Email> { firstEmail, secondEmail, thirdEmail };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([secondEmail]);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        repoMock.Verify(r => r.UpdateSendStatus(firstEmail.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatus(secondEmail.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(thirdEmail.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_EmptyBatch_PublisherNotCalled()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_MultipleBatches_PublisherCalledForEachBatch()
    {
        // Arrange
        var firstBatch = new List<Email> { _email, _email };
        var secondBatch = new List<Email> { _email };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstBatch)
            .ReturnsAsync(secondBatch)
            .ReturnsAsync([]);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_CancellationAfterFetchBeforePublish_StatusResetForBatch()
    {
        // Arrange
        var emails = new List<Email> { _email, _email };
        using var cts = new CancellationTokenSource();

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()))
            .Callback<int, CancellationToken, SendingTimePolicy>((_, _, _) => cts.Cancel())
            .ReturnsAsync(emails);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cts.Token));

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(emails.Count));
    }

    [Fact]
    public async Task SendNotifications_PublisherThrowsOperationCanceled_StatusResetForBatch()
    {
        // Arrange
        var emails = new List<Email> { _email, _email, _email };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var cts = new CancellationTokenSource();
        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cts.Token));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(emails.Count));
    }

    [Fact]
    public async Task SendNotifications_PublisherThrowsInvalidOperationException_ExceptionPropagatesAndStatusReset()
    {
        // Arrange
        var emails = new List<Email> { _email };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<IReadOnlyList<Email>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Misconfiguration"));

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendNotifications(CancellationToken.None));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(emails.Count));
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
        repoMock.Setup(r => r.UpdateSendStatus(
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
            r => r.UpdateSendStatus(
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
        repoMock.Setup(r => r.UpdateSendStatus(
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
            r => r.UpdateSendStatus(
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
        repoMock.Setup(r => r.UpdateSendStatus(
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
            r => r.UpdateSendStatus(
                It.Is<Guid>(n => n == notificationId),
                It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.New),
                It.Is<string>(s => s.Equals(operationId)),
                It.Is<string?>(d => d == deliveryReport)),
            Times.Once);
    }

    private EmailNotificationService GetTestService(IEmailNotificationRepository? repo = null, IKafkaProducer? producer = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null, IEmailCommandPublisher? emailCommandPublisher = null, bool sendViaWolverine = false)
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

        return new EmailNotificationService(
            guidService.Object,
            dateTimeService.Object,
            emailCommandPublisher,
            Options.Create(new NotificationConfig { EmailPublishBatchSize = _publishBatchSize }),
            repo);
    }
}
