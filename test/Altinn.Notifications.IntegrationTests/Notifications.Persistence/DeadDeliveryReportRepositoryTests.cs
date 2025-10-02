using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class DeadDeliveryReportRepositoryTests() : IAsyncLifetime
{
    private readonly List<long> _createdIds = new();
    
    [Fact]
    public async Task AddDeadDeliveryReport_ShouldCompleteWithoutException()
    {
        // Arrange
        DeadDeliveryReportRepository repo = (DeadDeliveryReportRepository)ServiceUtil
            .GetServices([typeof(IDeadDeliveryReportRepository)])
            .First(s => s is DeadDeliveryReportRepository);

        var testReport = new DeadDeliveryReport
        {
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            AttemptCount = 1,
            DeliveryReport = "{}",
            Resolved = false,
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow
        };

        // Act
        var result = await repo.Insert(testReport, CancellationToken.None);
        _createdIds.Add(result);

        // Assert
        Assert.True(result > 0);
    }

    public async Task DisposeAsync()
    {
        if (_createdIds.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.deaddeliveryreports where id in ('{string.Join("','", _createdIds)}')";
            await PostgreUtil.RunSql(deleteSql);
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
