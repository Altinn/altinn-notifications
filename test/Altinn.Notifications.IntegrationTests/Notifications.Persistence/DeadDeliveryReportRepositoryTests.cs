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

public class DeadDeliveryReportRepositoryTests() : IAsyncLifetime
{
    private readonly List<long> _createdIds = [];

    [Fact]
    public async Task InsertDeadDeliveryReport_ShouldCompleteWithoutException()
    {
        // Arrange
        var sut = GetRepository();
        var now = DateTime.UtcNow;
        var deliveryReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = now,
            LastAttempt = now,
            Resolved = false,
            AttemptCount = 1
        };

        // Act
        var id = await sut.InsertAsync(deliveryReport, CancellationToken.None);
        _createdIds.Add(id);

        var reportVerify = await sut.GetAsync(id, CancellationToken.None);

        // Assert - Truncate to milliseconds for PostgreSQL compatibility
        var normalizedDeliveryReport = deliveryReport with 
        { 
            FirstSeen = TruncateToMilliseconds(deliveryReport.FirstSeen),
            LastAttempt = TruncateToMilliseconds(deliveryReport.LastAttempt)
        };
        
        var normalizedReportVerify = reportVerify with 
        { 
            FirstSeen = TruncateToMilliseconds(reportVerify.FirstSeen),
            LastAttempt = TruncateToMilliseconds(reportVerify.LastAttempt)
        };

        Assert.Equal(normalizedDeliveryReport, normalizedReportVerify);
    }

    [Fact]
    public async Task Insert_WithDifferentChannels_ShouldStoreBothChannelTypes()
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

        // Assert - Truncate to milliseconds for PostgreSQL compatibility
        var normalizedAzure = azureReport with
        {
            FirstSeen = TruncateToMilliseconds(azureReport.FirstSeen),
            LastAttempt = TruncateToMilliseconds(azureReport.LastAttempt)
        };

        var normalizedLink = linkMobilityReport with
        {
            FirstSeen = TruncateToMilliseconds(linkMobilityReport.FirstSeen),
            LastAttempt = TruncateToMilliseconds(linkMobilityReport.LastAttempt)
        };

        var normalizedAzureVerify = azureReportVerify with
        {
            FirstSeen = TruncateToMilliseconds(azureReportVerify.FirstSeen),
            LastAttempt = TruncateToMilliseconds(azureReportVerify.LastAttempt)
        };

        var normalizedLinkMobilityVerify = linkMobilityReportVerify with
        {
            FirstSeen = TruncateToMilliseconds(linkMobilityReportVerify.FirstSeen),
            LastAttempt = TruncateToMilliseconds(linkMobilityReportVerify.LastAttempt)
        };

        Assert.Equal(normalizedAzure, normalizedAzureVerify);
        Assert.Equal(normalizedLink, normalizedLinkMobilityVerify);
    }

    [Fact]
    public async Task Insert_WithMinAttemptCount_ShouldStoreCorrectly()
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
        
        var normalizedVerify = reportVerify with 
        { 
            FirstSeen = TruncateToMilliseconds(reportVerify.FirstSeen),
            LastAttempt = TruncateToMilliseconds(reportVerify.LastAttempt)
        };

        var normalizedReport = report with 
        { 
            FirstSeen = TruncateToMilliseconds(report.FirstSeen),
            LastAttempt = TruncateToMilliseconds(report.LastAttempt)
        };  

        Assert.Equal(normalizedReport, normalizedVerify);
    }

    [Fact]
    public async Task Insert_WithInvalidAttemptCount_ShouldThrowDatabaseException()
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

    private static DeadDeliveryReportRepository GetRepository()
    {
        return (DeadDeliveryReportRepository)ServiceUtil
            .GetServices([typeof(IDeadDeliveryReportRepository)])
            .First(s => s is DeadDeliveryReportRepository);
    }

    // Truncate DateTime to milliseconds for PostgreSQL compatibility so records can be compared correctly
    private static DateTime TruncateToMilliseconds(DateTime dateTime)
    {
        long ticks = dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTime(ticks, dateTime.Kind);
    }

    [Fact]
    public async Task Insert_WithSerializedEmailSendOperationResult_ShouldStoreAndRetrieveCorrectly()
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

        Assert.NotNull(deliveryReportDeserialized);
        Assert.Equal(emailSendOperationResult.NotificationId, deliveryReportDeserialized.NotificationId);
        Assert.Equal(emailSendOperationResult.OperationId, deliveryReportDeserialized.OperationId);
        Assert.Equal(emailSendOperationResult.SendResult, deliveryReportDeserialized.SendResult);
    }

    public async Task DisposeAsync()
    {
        if (_createdIds.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.deaddeliveryreports where id = ANY(@ids)";
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
