using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;
using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class MetricsRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete = [];

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_orderIdsToDelete.Count == 0)
        {
            return;
        }

        foreach (var id in _orderIdsToDelete)
        {
            await PostgreUtil.DeleteOrderFromDb(id);
        }
    }

    [Fact]
    public async Task GetMetricsWithoutInflatingCounts_OneOrderWithMultipleNotifications_CountsOrderOncePerType()
    {
        // Arrange
        var orgName = $"InflationTest-{Guid.NewGuid():N}";
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        NotificationOrder order = await PostgreUtil.PopulateDBWithOrderAnd4Notifications(orgName);
        _orderIdsToDelete.Add(order.Id);

        // Act
        var result = await sut.GetMonthlyMetrics(DateTime.UtcNow.Month, DateTime.UtcNow.Year);

        // Assert
        var metrics = result.Metrics.FirstOrDefault(m => m.Org == orgName);
        Assert.NotNull(metrics);

        // Verify that we have the data we inserted
        Assert.Equal(1, metrics.OrdersCreated);
        Assert.Equal(2, metrics.SmsNotificationsCreated); // 2 SMS notifications per order found
        Assert.Equal(2, metrics.EmailNotificationsCreated); // 2 Email notifications per order found
    }

    [Fact]
    public async Task GetDailySmsMetrics()
    {
        // Arrange
        var orgName = $"test-{Guid.NewGuid():N}";
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        NotificationOrder order = await PostgreUtil.PopulateDBWithOrderAnd4Notifications(orgName);
        _orderIdsToDelete.Add(order.Id);

        // Act
        var result = await sut.GetDailySmsMetrics(DateTime.UtcNow.Day, DateTime.UtcNow.Month, DateTime.UtcNow.Year);

        // Assert
        Assert.Equal(2, result.Metrics.Count);
        var metrics = result.Metrics.FirstOrDefault();
        Assert.NotNull(metrics);

        Assert.NotNull(metrics.Rate);
        Assert.Equal("innland", metrics.Rate);
        Assert.Equal("+479", metrics.MobileNumberPrefix);
        Assert.Equal(orgName, metrics.CreatorName);
    }
}
