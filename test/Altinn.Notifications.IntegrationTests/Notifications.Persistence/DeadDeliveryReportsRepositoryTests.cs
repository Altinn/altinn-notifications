using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class DeadDeliveryReportsRepositoryTests : IAsyncLifetime
{
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

        // Act & Assert - Should not throw any exception
        var exception = await Record.ExceptionAsync(async () =>
            await repo.Add(testReport, CancellationToken.None));
        
        Assert.Null(exception);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
