using Altinn.Notifications.Core.Models.Notification;
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

        string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
        await PostgreUtil.RunSql(deleteSql);
    }

    [Fact]
    public async Task GetMonthlyMetrics_WithTwoSmsAndTwoEmailNotifications_ReturnsCorrectMetrics()
    {
        // Arrange
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        // Create first SMS notification
        (NotificationOrder smsOrder1, SmsNotification _) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToDelete.Add(smsOrder1.Id);

        // Create second SMS notification
        (NotificationOrder smsOrder2, SmsNotification _) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
        _orderIdsToDelete.Add(smsOrder2.Id);

        // Create first email notification
        (NotificationOrder emailOrder1, EmailNotification _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(emailOrder1.Id);

        // Create second email notification
        (NotificationOrder emailOrder2, EmailNotification _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
        _orderIdsToDelete.Add(emailOrder2.Id);

        // The month and year are determined by the hardcoded date in the test data
        int month = 6;
        int year = 2023;

        // Act
        var result = await sut.GetMonthlyMetrics(month, year);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(month, result.Month);
        Assert.Equal(year, result.Year);
        Assert.NotEmpty(result.Metrics);

        // Find metrics for the "ttd" organization
        var ttdMetrics = result.Metrics.FirstOrDefault(m => m.Org == "ttd");
        Assert.NotNull(ttdMetrics);

        // Verify that we have at least the data we inserted
        Assert.True(ttdMetrics.OrdersCreated >= 4); // At least 4 orders (2 SMS + 2 Email)
        Assert.True(ttdMetrics.SmsNotificationsCreated >= 2); // At least 2 SMS notifications
        Assert.True(ttdMetrics.EmailNotificationsCreated >= 2); // At least 2 Email notifications

        // Verify the organization name
        Assert.Equal("ttd", ttdMetrics.Org);
    }

    [Fact]
    public async Task GetMetricsWithoutInflatingCounts_OneOrderWithMultipleNotifications_CountsOrderOncePerType()
    {
        // Arrange
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        NotificationOrder order = await PostgreUtil.PopulateDBWithOrderAnd4Notifications();
        _orderIdsToDelete.Add(order.Id);

        // Act
        var result = await sut.GetMonthlyMetrics(6, 2023);

        // Assert
        var metrics = result.Metrics.FirstOrDefault(m => m.Org == "InflationTest");
        Assert.NotNull(metrics);

        // Verify that we have the data we inserted
        Assert.Equal(1, metrics.OrdersCreated);
        Assert.Equal(2, metrics.SmsNotificationsCreated); // 2 SMS notifications
        Assert.Equal(2, metrics.EmailNotificationsCreated); // 2 Email notifications
    }
}
