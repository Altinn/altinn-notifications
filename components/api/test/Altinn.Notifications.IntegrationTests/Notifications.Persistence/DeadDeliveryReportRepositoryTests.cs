using System.Text.Json;
using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class DeadDeliveryReportRepositoryTests : IAsyncLifetime
{
    private readonly List<long> _createdIds = [];

    [Fact]
    public async Task InsertAsync_WithValidReport_ReturnsId()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Reason = "RETRY_THRESHOLD_EXCEEDED",
            Message = "Test message"
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public async Task InsertAsync_WithDifferentChannels_ShouldStoreBothChannelTypes()
    {
        // Arrange
        var sut = GetRepository();
        var azureReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1
        };

        var linkMobilityReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.LinkMobility,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1
        };

        // Act
        var azureId = await sut.InsertAsync(azureReport, CancellationToken.None);
        var linkId = await sut.InsertAsync(linkMobilityReport, CancellationToken.None);

        _createdIds.AddRange([azureId, linkId]);

        // Assert
        var azureReportVerify = await sut.GetAsync(azureId, CancellationToken.None);
        var linkMobilityReportVerify = await sut.GetAsync(linkId, CancellationToken.None);

        Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, azureReportVerify.Channel);
        Assert.Equal(DeliveryReportChannel.LinkMobility, linkMobilityReportVerify.Channel);
    }

    [Fact]
    public async Task InsertAsync_WithMinAttemptCount_ShouldStoreCorrectly()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Assert
        var reportVerify = await sut.GetAsync(id, CancellationToken.None);
        Assert.Equal(1, reportVerify.AttemptCount);
    }

    [Fact]
    public async Task InsertAsync_WithHighAttemptCount_ShouldStoreCorrectly()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 100
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Assert
        var reportVerify = await sut.GetAsync(id, CancellationToken.None);
        Assert.Equal(100, reportVerify.AttemptCount);
    }

    [Fact]
    public async Task InsertAsync_WithInvalidAttemptCount_ShouldThrowDatabaseException()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 0 // invalid, should be > 0
        };

        // Act & Assert
        await Assert.ThrowsAsync<PostgresException>(() => sut.InsertAsync(report, CancellationToken.None));
    }

    [Fact]
    public async Task InsertAsync_WithNullReason_ShouldStoreCorrectly()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Reason = null
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Assert
        var reportVerify = await sut.GetAsync(id, CancellationToken.None);
        Assert.Null(reportVerify.Reason);
    }

    [Fact]
    public async Task InsertAsync_WithNullMessage_ShouldStoreCorrectly()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Message = null
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Assert
        var reportVerify = await sut.GetAsync(id, CancellationToken.None);
        Assert.Null(reportVerify.Message);
    }

    [Fact]
    public async Task InsertAsync_WithResolvedTrue_ShouldStoreCorrectly()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = true,
            AttemptCount = 1
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Assert
        var reportVerify = await sut.GetAsync(id, CancellationToken.None);
        Assert.True(reportVerify.Resolved);
    }

    [Fact]
    public async Task InsertAsync_WithSerializedEmailSendOperationResult_ShouldStoreAndRetrieveCorrectly()
    {
        // Arrange
        var sut = GetRepository();
        var emailSendOperationResult = new EmailSendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Delivered
        };

        var serializedEmailSendOperationResult = emailSendOperationResult.Serialize();

        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = serializedEmailSendOperationResult,
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow.AddMinutes(10),
            Resolved = false,
            AttemptCount = 1
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        var persistedReport = await sut.GetAsync(id, CancellationToken.None);
        var deliveryReportDeserialized = JsonSerializer.Deserialize<EmailSendOperationResult>(persistedReport.DeliveryReport, JsonSerializerOptionsProvider.Options);

        // Assert
        Assert.NotNull(deliveryReportDeserialized);
        Assert.Equal(emailSendOperationResult.NotificationId, deliveryReportDeserialized.NotificationId);
        Assert.Equal(emailSendOperationResult.OperationId, deliveryReportDeserialized.OperationId);
        Assert.Equal(emailSendOperationResult.SendResult, deliveryReportDeserialized.SendResult);
    }

    [Fact]
    public async Task InsertAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.InsertAsync(report, cts.Token));
    }

    [Fact]
    public async Task GetAsync_WithValidId_ReturnsReport()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{\"test\": \"data\"}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 5,
            Reason = "RETRY_THRESHOLD_EXCEEDED",
            Message = "Failed after multiple retries"
        };

        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Act
        var result = await sut.GetAsync(id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(report.Channel, result.Channel);
        Assert.Equal(report.DeliveryReport, result.DeliveryReport);
        Assert.Equal(report.Resolved, result.Resolved);
        Assert.Equal(report.AttemptCount, result.AttemptCount);
        Assert.Equal(report.Reason, result.Reason);
        Assert.Equal(report.Message, result.Message);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var sut = GetRepository();
        var nonExistentId = 999999999L;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.GetAsync(nonExistentId, CancellationToken.None));

        Assert.Contains(nonExistentId.ToString(), exception.Message);
    }

    [Fact]
    public async Task GetAsync_WithNullOptionalFields_ReturnsReportWithNulls()
    {
        // Arrange
        var sut = GetRepository();
        var report = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.LinkMobility,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Reason = null,
            Message = null
        };

        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Act
        var result = await sut.GetAsync(id, CancellationToken.None);

        // Assert
        Assert.Null(result.Reason);
        Assert.Null(result.Message);
    }
    
    [Fact]
    public async Task GetAllAsync_WithMatchingReports_ReturnsFilteredList()
    {
        // Arrange
        var sut = GetRepository();
        var reason = "RETRY_THRESHOLD_EXCEEDED";
        var channel = DeliveryReportChannel.AzureCommunicationServices;

        var report1 = new DeadDeliveryReport
        {
            Channel = channel,
            DeliveryReport = "{\"id\": 1}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Reason = reason
        };

        var report2 = new DeadDeliveryReport
        {
            Channel = channel,
            DeliveryReport = "{\"id\": 2}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 2,
            Reason = reason
        };

        var id1 = await sut.InsertAsync(report1, CancellationToken.None);
        var id2 = await sut.InsertAsync(report2, CancellationToken.None);
        _createdIds.AddRange([id1, id2]);

        // Act
        var results = await sut.GetAllAsync(id1, id2 + 1, reason, channel, CancellationToken.None);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(channel, r.Channel));
        Assert.All(results, r => Assert.Equal(reason, r.Reason));
    }

    [Fact]
    public async Task GetAllAsync_WithNoMatchingReports_ReturnsEmptyList()
    {
        // Arrange
        var sut = GetRepository();
        var reason = "NON_EXISTENT_REASON";
        var channel = DeliveryReportChannel.AzureCommunicationServices;

        // Act
        var results = await sut.GetAllAsync(1, 100, reason, channel, CancellationToken.None);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetAllAsync_WithDifferentChannels_FiltersCorrectly()
    {
        // Arrange
        var sut = GetRepository();
        var reason = "TEST_REASON";

        var azureReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Reason = reason
        };

        var smsReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.LinkMobility,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Reason = reason
        };

        var azureId = await sut.InsertAsync(azureReport, CancellationToken.None);
        var smsId = await sut.InsertAsync(smsReport, CancellationToken.None);
        _createdIds.AddRange([azureId, smsId]);

        var minId = Math.Min(azureId, smsId);
        var maxId = Math.Max(azureId, smsId) + 1;

        // Act
        var azureResults = await sut.GetAllAsync(minId, maxId, reason, DeliveryReportChannel.AzureCommunicationServices, CancellationToken.None);
        var smsResults = await sut.GetAllAsync(minId, maxId, reason, DeliveryReportChannel.LinkMobility, CancellationToken.None);

        // Assert
        Assert.All(azureResults, r => Assert.Equal(DeliveryReportChannel.AzureCommunicationServices, r.Channel));
        Assert.All(smsResults, r => Assert.Equal(DeliveryReportChannel.LinkMobility, r.Channel));
    }

    [Fact]
    public async Task GetAllAsync_WithIdRange_ReturnsOnlyReportsInRange()
    {
        // Arrange
        var sut = GetRepository();
        var reason = "RANGE_TEST";
        var channel = DeliveryReportChannel.AzureCommunicationServices;

        var report1 = new DeadDeliveryReport
        {
            Channel = channel,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 1,
            Reason = reason
        };

        var report2 = new DeadDeliveryReport
        {
            Channel = channel,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 2,
            Reason = reason
        };

        var report3 = new DeadDeliveryReport
        {
            Channel = channel,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            Resolved = false,
            AttemptCount = 3,
            Reason = reason
        };

        var id1 = await sut.InsertAsync(report1, CancellationToken.None);
        var id2 = await sut.InsertAsync(report2, CancellationToken.None);
        var id3 = await sut.InsertAsync(report3, CancellationToken.None);
        _createdIds.AddRange([id1, id2, id3]);

        // Act - Get only the middle report
        var results = await sut.GetAllAsync(id2, id2 + 1, reason, channel, CancellationToken.None);

        // Assert
        Assert.Single(results);
        Assert.Equal(2, results[0].AttemptCount);
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleReports_ReturnsAllMatching()
    {
        // Arrange
        var sut = GetRepository();
        var reason = "MULTIPLE_TEST";
        var channel = DeliveryReportChannel.AzureCommunicationServices;

        var reports = new List<DeadDeliveryReport>();
        for (int i = 0; i < 5; i++)
        {
            reports.Add(new DeadDeliveryReport
            {
                Channel = channel,
                DeliveryReport = $"{{\"index\": {i}}}",
                FirstSeen = DateTime.UtcNow,
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                AttemptCount = i + 1,
                Reason = reason
            });
        }

        var ids = new List<long>();
        foreach (var report in reports)
        {
            var id = await sut.InsertAsync(report, CancellationToken.None);
            ids.Add(id);
        }

        _createdIds.AddRange(ids);

        // Act
        var results = await sut.GetAllAsync(ids.Min(), ids.Max() + 1, reason, channel, CancellationToken.None);

        // Assert
        Assert.Equal(5, results.Count);
    }

    private static DeadDeliveryReportRepository GetRepository()
    {
        return (DeadDeliveryReportRepository)ServiceUtil
            .GetServices([typeof(IDeadDeliveryReportRepository)])
            .First(s => s is DeadDeliveryReportRepository);
    }

    public async Task DisposeAsync()
    {
        if (_createdIds.Count != 0)
        {
            string deleteSql = @"DELETE FROM notifications.deaddeliveryreports WHERE id = ANY(@ids)";
            NpgsqlParameter[] parameters =
            [
                new("ids", _createdIds.ToArray())
            ];

            await PostgreUtil.RunSql(deleteSql, parameters);
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
