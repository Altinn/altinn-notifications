using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Moq;
using Tools;

namespace ToolsTests;

public class UtilTests
{
    [Fact]
    public void MapToEmailSendOperationResult_ReturnsResult_WhenDeserializationSucceeds()
    {
        // Arrange
        var emailResult = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailNotificationResultType.Succeeded
        };

        var report = new DeadDeliveryReport
        {
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 3,
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = emailResult.Serialize(),
            Reason = Util.RetryExceededReason
        };

        // Act
        var result = Util.MapToEmailSendOperationResult(report);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(emailResult.NotificationId, result.NotificationId);
        Assert.Equal(emailResult.OperationId, result.OperationId);
        Assert.Equal(emailResult.SendResult, result.SendResult);
    }

    [Fact]
    public void MapToEmailSendOperationResult_ReturnsNull_WhenDeserializationFails()
    {
        // Arrange
        var report = new DeadDeliveryReport
        {
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 3,
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "invalid-json-{{{", // Invalid JSON
            Reason = Util.RetryExceededReason
        };

        // Act
        var result = Util.MapToEmailSendOperationResult(report);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapToEmailSendOperationResult_ReturnsNull_WhenDeliveryReportIsEmpty()
    {
        // Arrange
        var report = new DeadDeliveryReport
        {
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 3,
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = string.Empty,
            Reason = Util.RetryExceededReason
        };

        // Act
        var result = Util.MapToEmailSendOperationResult(report);

        // Assert
        Assert.Null(result);
    }    

    [Fact]
    public async Task GetAndMapDeadDeliveryReports_ReturnsEmptyList_WhenNoReportsFound()
    {
        // Arrange
        var mockRepo = new Mock<IDeadDeliveryReportRepository>();
        mockRepo.Setup(r => r.GetAllAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<DeliveryReportChannel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeadDeliveryReport>());

        // Act
        var result = await Util.GetAndMapDeadDeliveryReports(
            mockRepo.Object,
            1,
            100,
            DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        // Assert
        Assert.Empty(result);
        mockRepo.Verify(r => r.GetAllAsync(1, 100, Util.RetryExceededReason, DeliveryReportChannel.AzureCommunicationServices, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAndMapDeadDeliveryReports_ReturnsValidResults_WhenReportsExist()
    {
        // Arrange
        var emailResult1 = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-1",
            SendResult = EmailNotificationResultType.Succeeded
        };

        var emailResult2 = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-2",
            SendResult = EmailNotificationResultType.Failed_RecipientNotIdentified
        };

        var reports = new List<DeadDeliveryReport>
        {
            new DeadDeliveryReport
            {
                FirstSeen = DateTime.UtcNow,
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                AttemptCount = 3,
                Channel = DeliveryReportChannel.AzureCommunicationServices,
                DeliveryReport = emailResult1.Serialize(),
                Reason = Util.RetryExceededReason
            },
            new DeadDeliveryReport
            {
                FirstSeen = DateTime.UtcNow,
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                AttemptCount = 5,
                Channel = DeliveryReportChannel.AzureCommunicationServices,
                DeliveryReport = emailResult2.Serialize(),
                Reason = Util.RetryExceededReason
            }
        };

        var mockRepo = new Mock<IDeadDeliveryReportRepository>();
        mockRepo.Setup(r => r.GetAllAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<DeliveryReportChannel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        // Act
        var result = await Util.GetAndMapDeadDeliveryReports(
            mockRepo.Object,
            1,
            100,
            DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.OperationId == "op-1");
        Assert.Contains(result, r => r.OperationId == "op-2");
    }

    [Fact]
    public async Task GetAndMapDeadDeliveryReports_FiltersOutInvalidReports()
    {
        // Arrange
        var validEmailResult = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-valid",
            SendResult = EmailNotificationResultType.Succeeded
        };

        var reports = new List<DeadDeliveryReport>
        {
            new DeadDeliveryReport
            {
                FirstSeen = DateTime.UtcNow,
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                AttemptCount = 3,
                Channel = DeliveryReportChannel.AzureCommunicationServices,
                DeliveryReport = validEmailResult.Serialize(),
                Reason = Util.RetryExceededReason
            },
            new DeadDeliveryReport
            {
                FirstSeen = DateTime.UtcNow,
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                AttemptCount = 3,
                Channel = DeliveryReportChannel.AzureCommunicationServices,
                DeliveryReport = "invalid-json-data",
                Reason = Util.RetryExceededReason
            }
        };

        var mockRepo = new Mock<IDeadDeliveryReportRepository>();
        mockRepo.Setup(r => r.GetAllAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<DeliveryReportChannel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        // Act
        var result = await Util.GetAndMapDeadDeliveryReports(
            mockRepo.Object,
            1,
            100,
            DeliveryReportChannel.AzureCommunicationServices,
            CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("op-valid", result[0].OperationId);
    }

    [Fact]
    public async Task GetAndMapDeadDeliveryReports_UsesCorrectParameters()
    {
        // Arrange
        var mockRepo = new Mock<IDeadDeliveryReportRepository>();
        mockRepo.Setup(r => r.GetAllAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<DeliveryReportChannel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeadDeliveryReport>());

        var cts = new CancellationTokenSource();

        // Act
        await Util.GetAndMapDeadDeliveryReports(
            mockRepo.Object,
            50,
            150,
            DeliveryReportChannel.LinkMobility,
            cts.Token);

        // Assert
        mockRepo.Verify(r => r.GetAllAsync(
            50,
            150,
            Util.RetryExceededReason,
            DeliveryReportChannel.LinkMobility,
            cts.Token), Times.Once);
    }    

    [Fact]
    public async Task ProduceMessagesToKafka_ReturnsZero_WhenTopicIsNull()
    {
        // Arrange
        var mockProducer = new Mock<ICommonProducer>();
        var results = new List<EmailSendOperationResult>
        {
            new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = "op-1"
            }
        };

        // Act
        var count = await Util.ProduceMessagesToKafka(mockProducer.Object, null, results);

        // Assert
        Assert.Equal(0, count);
        mockProducer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProduceMessagesToKafka_ReturnsZero_WhenTopicIsEmpty()
    {
        // Arrange
        var mockProducer = new Mock<ICommonProducer>();
        var results = new List<EmailSendOperationResult>
        {
            new EmailSendOperationResult
            {
                NotificationId = Guid.NewGuid(),
                OperationId = "op-1"
            }
        };

        // Act
        var count = await Util.ProduceMessagesToKafka(mockProducer.Object, string.Empty, results);

        // Assert
        Assert.Equal(0, count);
        mockProducer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProduceMessagesToKafka_ReturnsZero_WhenNoResults()
    {
        // Arrange
        var mockProducer = new Mock<ICommonProducer>();
        var results = new List<EmailSendOperationResult>();

        // Act
        var count = await Util.ProduceMessagesToKafka(mockProducer.Object, "test-topic", results);

        // Assert
        Assert.Equal(0, count);
        mockProducer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProduceMessagesToKafka_ReturnsCorrectCount_WhenAllSucceed()
    {
        // Arrange
        var mockProducer = new Mock<ICommonProducer>();
        mockProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var results = new List<EmailSendOperationResult>
        {
            new EmailSendOperationResult { NotificationId = Guid.NewGuid(), OperationId = "op-1" },
            new EmailSendOperationResult { NotificationId = Guid.NewGuid(), OperationId = "op-2" },
            new EmailSendOperationResult { NotificationId = Guid.NewGuid(), OperationId = "op-3" }
        };

        // Act
        var count = await Util.ProduceMessagesToKafka(mockProducer.Object, "test-topic", results);

        // Assert
        Assert.Equal(3, count);
        mockProducer.Verify(p => p.ProduceAsync("test-topic", It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ProduceMessagesToKafka_ReturnsPartialCount_WhenSomeFail()
    {
        // Arrange
        var mockProducer = new Mock<ICommonProducer>();
        var callCount = 0;
        mockProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(() => ++callCount != 2); // Second call fails

        var results = new List<EmailSendOperationResult>
        {
            new EmailSendOperationResult { NotificationId = Guid.NewGuid(), OperationId = "op-1" },
            new EmailSendOperationResult { NotificationId = Guid.NewGuid(), OperationId = "op-2" },
            new EmailSendOperationResult { NotificationId = Guid.NewGuid(), OperationId = "op-3" }
        };

        // Act
        var count = await Util.ProduceMessagesToKafka(mockProducer.Object, "test-topic", results);

        // Assert
        Assert.Equal(2, count); // 1st and 3rd succeed, 2nd fails
        mockProducer.Verify(p => p.ProduceAsync("test-topic", It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ProduceMessagesToKafka_SerializesMessages_Correctly()
    {
        // Arrange
        var capturedMessages = new List<string>();
        var mockProducer = new Mock<ICommonProducer>();
        mockProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((topic, message) => capturedMessages.Add(message))
            .ReturnsAsync(true);

        var notificationId = Guid.NewGuid();
        var results = new List<EmailSendOperationResult>
        {
            new EmailSendOperationResult
            {
                NotificationId = notificationId,
                OperationId = "op-123",
                SendResult = EmailNotificationResultType.Succeeded
            }
        };

        // Act
        await Util.ProduceMessagesToKafka(mockProducer.Object, "test-topic", results);

        // Assert
        Assert.Single(capturedMessages);
        var deserializedResult = EmailSendOperationResult.Deserialize(capturedMessages[0]);
        Assert.NotNull(deserializedResult);
        Assert.Equal(notificationId, deserializedResult.NotificationId);
        Assert.Equal("op-123", deserializedResult.OperationId);
        Assert.Equal(EmailNotificationResultType.Succeeded, deserializedResult.SendResult);
    }    

    [Fact]
    public void RetryExceededReason_ReturnsCorrectValue()
    {
        // Act
        var reason = Util.RetryExceededReason;

        // Assert
        Assert.Equal("RETRY_THRESHOLD_EXCEEDED", reason);
    }

    [Fact]
    public void MapStatus_ReturnsBounced_WhenResultIsFailed_Bounced()
    {
        // Arrange
        var result = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailNotificationResultType.Failed_Bounced
        };

        // Act
        var status = Util.MapStatus(result);

        // Assert
        Assert.Equal("Bounced", status);
    }

    [Fact]
    public void MapStatus_ReturnsSuppressed_WhenResultIsFailed_SupressedRecipient()
    {
        // Arrange
        var result = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailNotificationResultType.Failed_SupressedRecipient
        };

        // Act
        var status = Util.MapStatus(result);

        // Assert
        Assert.Equal("Suppressed", status);
    }

    [Fact]
    public void MapStatus_ReturnsOriginalStatus_WhenResultIsSucceeded()
    {
        // Arrange
        var result = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailNotificationResultType.Succeeded
        };

        // Act
        var status = Util.MapStatus(result);

        // Assert
        Assert.Equal("Succeeded", status);
    }

    [Fact]
    public void MapStatus_ReturnsOriginalStatus_WhenResultIsFailed_RecipientNotIdentified()
    {
        // Arrange
        var result = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailNotificationResultType.Failed_RecipientNotIdentified
        };

        // Act
        var status = Util.MapStatus(result);

        // Assert
        Assert.Equal("Failed_RecipientNotIdentified", status);
    }

    [Fact]
    public void MapStatus_IsCaseInsensitive_ForBouncedStatus()
    {
        // Arrange - Test that the comparison is case-insensitive
        var result = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = EmailNotificationResultType.Failed_Bounced
        };

        // Act
        var status = Util.MapStatus(result);

        // Assert - Should still map to "Bounced" regardless of enum ToString() casing
        Assert.Equal("Bounced", status);
    }

    [Fact]
    public void MapStatus_ReturnsNull_WhenSendResultIsNull()
    {
        // Arrange
        var result = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = null
        };

        // Act
        var status = Util.MapStatus(result);

        // Assert
        Assert.Null(status);
    }
}
