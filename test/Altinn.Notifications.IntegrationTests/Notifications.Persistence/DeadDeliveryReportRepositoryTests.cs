using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Npgsql;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class DeadDeliveryReportRepositoryTests() : IAsyncLifetime
{
    private readonly List<long> _createdIds = [];

    [Fact]
    public async Task AddDeadDeliveryReport_ShouldCompleteWithoutException()
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

        var reportVerify = await sut.GetDeadDeliveryReportAsync(id, CancellationToken.None);

        // Assert - Truncate to microseconds for PostgreSQL compatibility
        var normalizedDeliveryReport = deliveryReport with 
        { 
            FirstSeen = TruncateToMicroseconds(deliveryReport.FirstSeen),
            LastAttempt = TruncateToMicroseconds(deliveryReport.LastAttempt)
        };
        
        var normalizedReportVerify = reportVerify with 
        { 
            FirstSeen = TruncateToMicroseconds(reportVerify.FirstSeen),
            LastAttempt = TruncateToMicroseconds(reportVerify.LastAttempt)
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
        var azureReportVerify = await sut.GetDeadDeliveryReportAsync(azureId, CancellationToken.None);
        var linkMobilityReportVerify = await sut.GetDeadDeliveryReportAsync(linkId, CancellationToken.None);

        // Assert - Truncate to microseconds for PostgreSQL compatibility
        var normalizedAzure = azureReport with
        {
            FirstSeen = TruncateToMicroseconds(azureReport.FirstSeen),
            LastAttempt = TruncateToMicroseconds(azureReport.LastAttempt)
        };

        var normalizedLink = linkMobilityReport with
        {
            FirstSeen = TruncateToMicroseconds(linkMobilityReport.FirstSeen),
            LastAttempt = TruncateToMicroseconds(linkMobilityReport.LastAttempt)
        };

        var normalizedAzureVerify = azureReportVerify with
        {
            FirstSeen = TruncateToMicroseconds(azureReportVerify.FirstSeen),
            LastAttempt = TruncateToMicroseconds(azureReportVerify.LastAttempt)
        };

        var normalizedLinkMobilityVerify = linkMobilityReport with
        {
            FirstSeen = TruncateToMicroseconds(linkMobilityReportVerify.FirstSeen),
            LastAttempt = TruncateToMicroseconds(linkMobilityReportVerify.LastAttempt)
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
        var reportVerify = await sut.GetDeadDeliveryReportAsync(id, CancellationToken.None);
        
        var normalizedVerify = reportVerify with 
        { 
            FirstSeen = TruncateToMicroseconds(reportVerify.FirstSeen),
            LastAttempt = TruncateToMicroseconds(reportVerify.LastAttempt)
        };

        var normalizedReport = report with 
        { 
            FirstSeen = TruncateToMicroseconds(report.FirstSeen),
            LastAttempt = TruncateToMicroseconds(report.LastAttempt)
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

    private static async Task<int> GetAttemptCountById(long id)
    {
        string sql = $"SELECT attemptcount FROM notifications.deaddeliveryreports WHERE id = {id}";
        return await PostgreUtil.RunSqlReturnOutput<int>(sql);
    }

    // Truncate DateTime to microseconds for PostgreSQL compatibility so records can be compared correctly
    private static DateTime TruncateToMicroseconds(DateTime dateTime)
    {
        long ticks = dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTime(ticks, dateTime.Kind);
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
