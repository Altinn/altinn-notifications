using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
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
    public async Task AddDeadDeliveryReport_ShouldCompleteWithoutException()
    {
        // Arrange
        var sut = GetRepository();
        var mockReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow
        };

        // Act
        var result = await sut.InsertAsync(mockReport, CancellationToken.None);
        _createdIds.Add(result);

        // Assert
        Assert.True(result > 0);
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
            LastAttempt = DateTime.UtcNow
        };

        var linkMobilityReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.LinkMobility,
            DeliveryReport = "{}",
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow
        };

        // Act
        var azureId = await sut.InsertAsync(azureReport, CancellationToken.None);
        var linkId = await sut.InsertAsync(linkMobilityReport, CancellationToken.None);

        _createdIds.AddRange([azureId, linkId]);

        // Assert
        var azureCount = await GetReportCountByChannel(DeliveryReportChannel.AzureCommunicationServices);
        var linkCount = await GetReportCountByChannel(DeliveryReportChannel.LinkMobility);

        Assert.True(azureCount >= 1);
        Assert.True(linkCount >= 1);
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
            AttemptCount = 1
        };

        // Act
        var id = await sut.InsertAsync(report, CancellationToken.None);
        _createdIds.Add(id);

        // Assert
        var storedAttemptCount = await GetAttemptCountById(id);
        Assert.Equal(1, storedAttemptCount);
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

    private static async Task<int> GetReportCountByChannel(DeliveryReportChannel channel)
    {
        string sql = $"SELECT COUNT(1) FROM notifications.deaddeliveryreports WHERE channel = {(short)channel}";
        return await PostgreUtil.RunSqlReturnOutput<int>(sql);
    }

    private static async Task<int> GetAttemptCountById(long id)
    {
        string sql = $"SELECT attemptcount FROM notifications.deaddeliveryreports WHERE id = {id}";
        return await PostgreUtil.RunSqlReturnOutput<int>(sql);
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
