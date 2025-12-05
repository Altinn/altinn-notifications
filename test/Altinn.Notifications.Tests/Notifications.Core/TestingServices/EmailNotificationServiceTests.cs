using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

public class EmailNotificationServiceTests
{
    private readonly int _publishBatchSize = 500;
    private const string _emailQueueTopicName = "email.queue";
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
        await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient);

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
        await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient);

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
        await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient, true);

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

        // Act
        await service.CreateNotification(orderId, requestedSendTime, emailAddressPoints, emailRecipient);

        // Assert
        repoMock.Verify();
    }

    [Fact]
    public async Task CreateNotification_RecipientHasTwoEmailAddresses_RepositoryCalledOnceForEachAddress()
    {
        // Arrange
        var emailRecipient = new EmailRecipient() { OrganizationNumber = "org" };
        var emailAddressPoints = new List<EmailAddressPoint>() { new("user_1@domain.com"), new("user_2@domain.com") };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.AddNotification(It.Is<EmailNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>()));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.CreateNotification(Guid.NewGuid(), DateTime.UtcNow, emailAddressPoints, emailRecipient);

        // Assert
        repoMock.Verify(r => r.AddNotification(It.Is<EmailNotification>(s => s.Recipient.OrganizationNumber == "org"), It.IsAny<DateTime>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateSendStatus_SendResultDefined_Succeded()
    {
        // Arrange
        Guid notificationid = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationid,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Succeeded
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(It.Is<Guid>(n => n == notificationid), It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.Succeeded), It.Is<string>(s => s.Equals(operationId))));

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
        Guid notificationid = Guid.NewGuid();
        string operationId = Guid.NewGuid().ToString();

        EmailSendOperationResult sendOperationResult = new()
        {
            NotificationId = notificationid,
            OperationId = operationId,
            SendResult = EmailNotificationResultType.Failed_TransientError
        };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock.Setup(r => r.UpdateSendStatus(
            It.Is<Guid>(n => n == notificationid),
            It.Is<EmailNotificationResultType>(e => e == EmailNotificationResultType.New),
            It.Is<string>(s => s.Equals(operationId))));

        var service = GetTestService(repo: repoMock.Object);

        // Act
        await service.UpdateSendStatus(sendOperationResult);

        // Assert
        repoMock.Verify();
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
    public async Task SendNotifications_CancellationAfterProduceNextFetch_NoStatusResets()
    {
        // Arrange
        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .SetupSequence(e => e.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_email, _email]) // First call returns emails to process
            .ThrowsAsync(new OperationCanceledException()); // Second call simulates cancellation

        var producer = new Mock<IKafkaProducer>();
        producer
            .Setup(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]); // Simulate successful produce

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object, producer: producer.Object);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cancellationTokenSource.Token));

        producer.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        emailNotificationRepositoryMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), It.IsAny<EmailNotificationResultType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_ProducerReturnsAllUnpublished_AllEmailsResetToNew()
    {
        // Arrange
        var firstEmailNotification = new Email(Guid.NewGuid(), "a", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var secondEmailNotification = new Email(Guid.NewGuid(), "b", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var thirdEmailNotification = new Email(Guid.NewGuid(), "c", "b", "from@d.com", "to@d.com", EmailContentType.Plain);

        var batch = new List<Email> { firstEmailNotification, secondEmailNotification, thirdEmailNotification };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .SetupSequence(e => e.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        // Producer returns all serialized emails as failed
        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(
                _emailQueueTopicName,
                It.Is<ImmutableList<string>>(m => m.Count == batch.Count),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstEmailNotification.Serialize(), secondEmailNotification.Serialize(), thirdEmailNotification.Serialize()]);

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        repoMock.Verify(e => e.UpdateSendStatus(firstEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(secondEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(thirdEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_ProducerThrowsOperationCanceled_StatusResetForBatch()
    {
        // Arrange
        List<Email> emails = [_email, _email, _email];

        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .Setup(e => e.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails); // Return a batch of emails

        var producer = new Mock<IKafkaProducer>();
        producer
            .Setup(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException()); // Simulate cancellation during produce

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object, producer: producer.Object);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cancellationTokenSource.Token));

        emailNotificationRepositoryMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(emails.Count));
    }

    [Fact]
    public async Task SendNotifications_CancellationAfterFetchBeforeProduce_StatusResetForBatch()
    {
        // Arrange
        List<Email> emails = [_email, _email];

        using var cancellationTokenSource = new CancellationTokenSource();

        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();
        emailNotificationRepositoryMock
            .Setup(e => e.GetNewNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int, CancellationToken>((_, _) => cancellationTokenSource.Cancel()) // Cancel after fetch
            .ReturnsAsync(emails);

        var producer = new Mock<IKafkaProducer>();

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object, producer: producer.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendNotifications(cancellationTokenSource.Token));

        producer.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        emailNotificationRepositoryMock.Verify(e => e.UpdateSendStatus(It.IsAny<Guid?>(), EmailNotificationResultType.New, It.IsAny<string?>()), Times.Exactly(emails.Count));
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
    public async Task SendNotifications_ProducerReturnsSubsetUnpublished_OnlyFailedEmailsResetToNew()
    {
        // Arrange
        var firstEmailNotification = new Email(Guid.NewGuid(), "s1", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var secondEmailNotification = new Email(Guid.NewGuid(), "s2", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var thirdEmailNotification = new Email(Guid.NewGuid(), "s3", "b", "from@d.com", "to@d.com", EmailContentType.Plain);

        var batch = new List<Email> { firstEmailNotification, secondEmailNotification, thirdEmailNotification };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .SetupSequence(e => e.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        // Simulate that only secondEmailNotification failed to publish (partial failure)
        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(e => e.ProduceAsync(
                It.Is<string>(e => e == _emailQueueTopicName),
                It.Is<ImmutableList<string>>(e => e.Count == batch.Count),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([secondEmailNotification.Serialize()]);

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        producerMock.Verify(e => e.ProduceAsync(_emailQueueTopicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(secondEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(firstEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
        repoMock.Verify(e => e.UpdateSendStatus(thirdEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SendNotifications_ProducerReturnsInvalidAndValidUnpublished_OnlyValidEmailsResetToNew()
    {
        // Arrange
        var firstEmailNotification = new Email(Guid.NewGuid(), "x", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var secondEmailNotification = new Email(Guid.NewGuid(), "y", "b", "from@d.com", "to@d.com", EmailContentType.Plain);
        var thirdEmailNotification = new Email(Guid.NewGuid(), "z", "b", "from@d.com", "to@d.com", EmailContentType.Plain);

        var batch = new List<Email> { firstEmailNotification, secondEmailNotification, thirdEmailNotification };

        var repoMock = new Mock<IEmailNotificationRepository>();
        repoMock
            .SetupSequence(e => e.GetNewNotificationsAsync(_publishBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);

        var invalidEntries = new[] { "{}" };
        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(e => e.ProduceAsync(
                _emailQueueTopicName,
                It.Is<ImmutableList<string>>(e => e.Count == batch.Count),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstEmailNotification.Serialize(), thirdEmailNotification.Serialize(), .. invalidEntries]);

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        repoMock.Verify(e => e.UpdateSendStatus(firstEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(thirdEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Once);
        repoMock.Verify(e => e.UpdateSendStatus(secondEmailNotification.NotificationId, EmailNotificationResultType.New, It.IsAny<string?>()), Times.Never);

        producerMock.Verify(e => e.ProduceAsync(_emailQueueTopicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotifications_PublishesSingleBatchContainingAllRetrievedEmails_StopsAfterEmptyFetch()
    {
        // Arrange
        var emptyEmailNotificationsBatch = new List<Email>();
        var filledEmailNotificationsBatch = new List<Email>() { _email, _email, _email };
        var emailNotificationRepositoryMock = new Mock<IEmailNotificationRepository>();

        emailNotificationRepositoryMock
            .SetupSequence(e => e.GetNewNotificationsAsync(_publishBatchSize, CancellationToken.None))
            .ReturnsAsync(filledEmailNotificationsBatch)
            .ReturnsAsync(emptyEmailNotificationsBatch);

        var kafkaProducerMock = new Mock<IKafkaProducer>();
        ImmutableList<string>? capturedEmailNotificationsBatch = null;

        kafkaProducerMock
            .Setup(e => e.ProduceAsync(
                It.Is<string>(e => e.Equals(_emailQueueTopicName)),
                It.IsAny<ImmutableList<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ImmutableList<string>, CancellationToken>((_, passedMessages, _) => capturedEmailNotificationsBatch = passedMessages)
            .ReturnsAsync([]);

        var service = GetTestService(repo: emailNotificationRepositoryMock.Object, producer: kafkaProducerMock.Object);

        // Act
        await service.SendNotifications(CancellationToken.None);

        // Assert
        emailNotificationRepositoryMock.Verify(e => e.GetNewNotificationsAsync(_publishBatchSize, CancellationToken.None), Times.Exactly(2));

        kafkaProducerMock.Verify(
            e => e.ProduceAsync(
            It.Is<string>(e => e.Equals(_emailQueueTopicName)),
            It.IsAny<ImmutableList<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedEmailNotificationsBatch);
        Assert.Equal(3, capturedEmailNotificationsBatch!.Count);
    }

    private EmailNotificationService GetTestService(IEmailNotificationRepository? repo = null, IKafkaProducer? producer = null, Guid? guidOutput = null, DateTime? dateTimeOutput = null)
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
            var repoMock = new Mock<IEmailNotificationRepository>();
            repo = repoMock.Object;
        }

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        return new EmailNotificationService(
            guidService.Object,
            producer,
            dateTimeService.Object,
            Options.Create(new KafkaSettings { EmailQueueTopicName = _emailQueueTopicName }),
            Options.Create(new NotificationConfig { EmailPublishBatchSize = _publishBatchSize }),
            repo);
    }
}
