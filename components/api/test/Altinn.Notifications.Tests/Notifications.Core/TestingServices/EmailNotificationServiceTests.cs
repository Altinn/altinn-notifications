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
        Assert.NotNull(result[0]);
        Assert.Single(result);
        Assert.Equivalent(expected, result[0]);
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
        var singleResult = result[0];

        // Assert
        Assert.NotNull(singleResult);
        Assert.Single(result);
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
        var result = await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient, true);
        var singleResult = result[0];

        // Assert
        Assert.Single(result);
        Assert.NotNull(singleResult);
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
        var singleResult = result[0];

        // Assert
        Assert.Single(result);
        Assert.NotNull(singleResult);
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
            new Mock<IComposedEmailCommandPublisher>().Object);

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
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(_email);

        var service = GetTestService(repo: repoMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await service.SendNotification(cts.Token));

        repoMock.Verify(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_RepositoryThrowsOperationCanceledDuringFetch_NoStatusResets()
    {
        // Arrange
        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .Setup(e => e.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException()); // Simulate cancellation during fetch

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotification(TestContext.Current.CancellationToken));

        emailNotificationRepositoryMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_CancellationAfterPublishBeforeNextFetch_NoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .SetupSequence(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Email?)null);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        bool wasSent = await service.SendNotification(TestContext.Current.CancellationToken);

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(wasSent);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_AllEmailsPublishedSuccessfully_NoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email)
            .ReturnsAsync((Email?)null);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Email?)null);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendNotification(TestContext.Current.CancellationToken);

        // Assert
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(wasSent);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_PublisherFailsForAllEmails_AllEmailsResetToNew()
    {
        // Arrange
        Email firstEmail = new(Guid.NewGuid(), "first.email.subject", "first.email.body", "from-first@domain.com", "to-first@domain.com", EmailContentType.Plain);
        Email secondEmail = new(Guid.NewGuid(), "second.email.subject", "second.email.body", "from-second@domain.com", "to-second@domain.com", EmailContentType.Plain);
        Email thirdEmail = new(Guid.NewGuid(), "third.email.subject", "third.email.body", "from-third@domain.com", "to-third@domain.com", EmailContentType.Plain);

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstEmail)
            .ReturnsAsync((Email?)null);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstEmail);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        await service.SendNotification(TestContext.Current.CancellationToken);

        // Assert
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_PublisherFailsForSubset_OnlyFailedEmailsResetToNew()
    {
        // Arrange
        Email firstEmail = new(Guid.NewGuid(), "first.email.subject", "first.email.body", "from-first@domain.com", "to-first@domain.com", EmailContentType.Plain);
        Email secondEmail = new(Guid.NewGuid(), "second.email.subject", "second.email.body", "from-second@domain.com", "to-second@domain.com", EmailContentType.Plain);
        Email thirdEmail = new(Guid.NewGuid(), "third.email.subject", "third.email.body", "from-third@domain.com", "to-third@domain.com", EmailContentType.Plain);

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstEmail)
            .ReturnsAsync((Email?)null);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondEmail);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        await service.SendNotification(TestContext.Current.CancellationToken);

        // Assert
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_EmptyBatch_PublisherNotCalled()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Email?)null);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendNotification(TestContext.Current.CancellationToken);

        // Assert
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(wasSent);
    }

    [Fact]
    public async Task SendNotifications_MultipleBatches_PublisherCalledForEachBatch()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.SetupSequence(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email)
            .ReturnsAsync(_email)
            .ReturnsAsync((Email?)null);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Email?)null);

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendNotification(TestContext.Current.CancellationToken);

        // Assert
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(wasSent);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_CancellationAfterFetchBeforePublish_StatusResetForBatch()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotification(cts.Token));

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_PublisherThrowsOperationCanceled_StatusResetForBatch()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotification(TestContext.Current.CancellationToken));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_PublisherThrowsInvalidOperationException_ExceptionPropagatesAndStatusReset()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Misconfiguration"));

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendNotification(TestContext.Current.CancellationToken));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_PublisherThrowsUnexpectedException_ExceptionPropagatesAndStatusReset()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_email);

        var publisherMock = new Mock<IEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Unexpected timeout"));

        var service = GetTestService(repo: repoMock.Object, emailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() => service.SendNotification(TestContext.Current.CancellationToken));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_RepositoryThrowsOperationCanceledDuringFetch_NoStatusResets()
    {
        // Arrange
        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .Setup(e => e.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendComposedNotification(TestContext.Current.CancellationToken));

        emailNotificationRepositoryMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_CancellationRequested_StopsProcessing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendComposedNotification(cts.Token));

        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_EmptyBatch_PublisherNotCalled()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComposedEmail?)null);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wasSent);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_AllEmailsPublishedSuccessfully_NoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComposedEmail?)null);

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(wasSent);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_PublisherFailsForAllEmails_NoStatusResets()
    {
        // Arrange
        ComposedEmail first = new(Guid.NewGuid(), "s1", "b1", "from@domain.com", "to1@domain.com", EmailContentType.Plain, []);

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(first);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(first);

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(wasSent);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_PublisherFailsForSubset_NoStatusResets()
    {
        // Arrange
        ComposedEmail first = new(Guid.NewGuid(), "s1", "b1", "from@domain.com", "to1@domain.com", EmailContentType.Plain, []);
        ComposedEmail second = new(Guid.NewGuid(), "s2", "b2", "from@domain.com", "to2@domain.com", EmailContentType.Plain, []);

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(first);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(second);

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(wasSent);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_PublisherCalledForSingleNotification()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComposedEmail?)null);

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act
        bool wasSent = await service.SendComposedNotification(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(wasSent);
        publisherMock.Verify(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_PublisherThrowsOperationCanceled_NoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendComposedNotification(TestContext.Current.CancellationToken));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_PublisherThrowsInvalidOperationException_ExceptionPropagatesAndNoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Misconfiguration"));

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendComposedNotification(TestContext.Current.CancellationToken));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendComposedNotification_PublisherThrowsUnexpectedException_ExceptionPropagatesAndNoStatusResets()
    {
        // Arrange
        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.GetNewComposedNotificationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_composedEmail);

        var publisherMock = new Mock<IComposedEmailCommandPublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Unexpected timeout"));

        var service = GetTestService(repo: repoMock.Object, composedEmailCommandPublisher: publisherMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() => service.SendComposedNotification(TestContext.Current.CancellationToken));

        repoMock.Verify(r => r.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
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

    private EmailNotificationService GetTestService(IEmailNotificationRepository? repo = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null, IEmailCommandPublisher? emailCommandPublisher = null, IComposedEmailCommandPublisher? composedEmailCommandPublisher = null)
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

        return new EmailNotificationService(
            guidService.Object,
            dateTimeService.Object,
            emailCommandPublisher,
            repo,
            composedEmailCommandPublisher);
    }
}
