using System.Data;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class MetricsRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete = [];

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_orderIdsToDelete.Count == 0)
        {
            return;
        }

        foreach (var id in _orderIdsToDelete)
        {
            await PostgreUtil.DeleteStatusFeedFromDb(id);
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
        Assert.Equal(0, metrics.SmsNotificationsCreated); // smscount column is no longer populated (#1661)
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

        var date = DateTime.UtcNow;

        NotificationOrder order = await PostgreUtil.PopulateDBWithOrderAnd4Notifications(orgName, date);
        _orderIdsToDelete.Add(order.Id);

        // Act
        var result = await sut.GetDailySmsMetrics(date.Day, date.Month, date.Year, TestContext.Current.CancellationToken);

        // Assert
        Assert.InRange(result.Metrics.Count, 2, int.MaxValue);

        var metrics = result.Metrics.FirstOrDefault(m => m.CreatorName == orgName);
        Assert.NotNull(metrics);
        Assert.Equal("innland", metrics.Rate);
        Assert.Equal("+479", metrics.MobileNumberPrefix);
        Assert.Equal(orgName, metrics.CreatorName);
        Assert.True(Guid.TryParse(metrics.SmsShipmentId, out _));
        Assert.Equal(order.Id.ToString(), metrics.OrderShipmentId);
        Assert.NotEqual(default, metrics.RequestedSendTime);
        Assert.NotEqual(default, metrics.ResultTime);
    }

    [Fact]
    public async Task GetDailyEmailMetrics()
    {
        // Arrange
        var orgName = $"test-{Guid.NewGuid():N}";
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        var date = DateTime.UtcNow;

        NotificationOrder order = await PostgreUtil.PopulateDBWithOrderAnd4Notifications(orgName, date);
        _orderIdsToDelete.Add(order.Id);

        // Act
        var result = await sut.GetDailyEmailMetrics(date.Day, date.Month, date.Year, TestContext.Current.CancellationToken);

        // Assert
        Assert.InRange(result.Metrics.Count, 2, int.MaxValue);

        var metrics = result.Metrics.FirstOrDefault(m => m.CreatorName == orgName);
        Assert.NotNull(metrics);
        Assert.Equal(orgName, metrics.CreatorName);
        Assert.NotEqual(default, metrics.RequestedSendTime);
        Assert.NotEqual(default, metrics.ResultTime);
        Assert.Equal("Delivered", metrics.Result);
        Assert.Equal("local-testing", metrics.SendersReference);
        Assert.Null(metrics.ResourceId);
        Assert.Empty(metrics.OperationId);
        Assert.True(Guid.TryParse(metrics.EmailShipmentId, out _));
        Assert.Equal(order.Id.ToString(), metrics.OrderShipmentId);
    }

    [Fact]
    public async Task GetEmailNotificationsMetrics_ReturnsNonNegativeCounts()
    {
        // Arrange
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        // Act
        var (anytimeCount, daytimeCount, composedCount) = await sut.GetEmailNotificationsMetrics(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(anytimeCount >= 0);
        Assert.True(daytimeCount >= 0);
        Assert.True(composedCount >= 0);
    }

    [Fact]
    public async Task GetSmsNotificationsMetrics_ReturnsNonNegativeCounts()
    {
        // Arrange
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        // Act
        var (anytimeCount, daytimeCount) = await sut.GetSmsNotificationsMetrics(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(anytimeCount >= 0);
        Assert.True(daytimeCount >= 0);
    }

    [Fact]
    public async Task GetOrderMetrics_ReturnsNonNegativeCounts()
    {
        // Arrange
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        // Act
        var (ordersNotDue, ordersPastDue, ordersFailed) = await sut.GetOrderMetrics(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ordersNotDue >= 0);
        Assert.True(ordersPastDue >= 0);
        Assert.True(ordersFailed >= 0);
    }

    [Fact]
    public async Task GetOrderMetrics_CountsFailedAndRetryExhaustedOrders()
    {
        NotificationOrder failedOrder = await PostgreUtil.PopulateDBWithEmailOrder();
        _orderIdsToDelete.Add(failedOrder.Id);
        NotificationOrder exhaustedOrder = await PostgreUtil.PopulateDBWithEmailOrder();
        _orderIdsToDelete.Add(exhaustedOrder.Id);
        NotificationOrder retryingOrder = await PostgreUtil.PopulateDBWithEmailOrder();
        _orderIdsToDelete.Add(retryingOrder.Id);

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using NpgsqlConnection connection = await ServiceUtil.GetSharedDataSource().OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        long baseline = await GetFailedCount();
        await SetStatus(failedOrder.Id, OrderProcessingStatus.Failed);
        Assert.Equal(baseline + 1, await GetFailedCount());

        await SetStatus(exhaustedOrder.Id, OrderProcessingStatus.RetryExhausted);
        Assert.Equal(baseline + 2, await GetFailedCount());

        await SetStatus(retryingOrder.Id, OrderProcessingStatus.Retrying);
        Assert.Equal(baseline + 2, await GetFailedCount());

        await SetStatus(exhaustedOrder.Id, OrderProcessingStatus.Failed);
        Assert.Equal(baseline + 2, await GetFailedCount());

        await SetStatus(exhaustedOrder.Id, OrderProcessingStatus.Retrying);
        Assert.Equal(baseline + 1, await GetFailedCount());

        async Task<long> GetFailedCount()
        {
            await using var command = new NpgsqlCommand("SELECT ordersfailedcount FROM notifications.get_order_metrics()", connection, transaction);
            return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        async Task SetStatus(Guid orderId, OrderProcessingStatus status)
        {
            await using var command = new NpgsqlCommand("UPDATE notifications.orders SET processedstatus = @status::orderprocessingstate WHERE alternateid = @id", connection, transaction);
            command.Parameters.AddWithValue("status", status.ToString());
            command.Parameters.AddWithValue("id", orderId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
        }
    }

    [Fact]
    public async Task GetOrderNotDueDistributionMetrics_ReturnsRowsWithNonNegativeCount()
    {
        // Arrange
        MetricsRepository sut = (MetricsRepository)ServiceUtil
            .GetServices([typeof(IMetricsRepository)])
            .First(i => i.GetType() == typeof(MetricsRepository));

        // Act
        List<(DateOnly Day, long Count)> result = await sut.GetOrderNotDueDistributionMetrics(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain(result, row => row.Count < 0);
    }
}
