using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTests.Utils;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    public sealed class MetricsServiceTests : IAsyncLifetime
    {
        private readonly List<Guid> _orderIdsToDelete;

        public MetricsServiceTests()
        {
            _orderIdsToDelete = new List<Guid>();
        }

        public ValueTask InitializeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await PostgreUtil.DeleteOrdersByAlternateIds(_orderIdsToDelete);
        }

        [Fact]
        public async Task GetMontlyNotificationMetrics_TwoOrdersPlacedForTTD()
        {
            // Arrange
            MetricsService service = (MetricsService)ServiceUtil
                .GetServices(new List<Type>() { typeof(IMetricsService) })
                .First(i => i.GetType() == typeof(MetricsService));

            (NotificationOrder smsOrder, _) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
            (NotificationOrder emailOrder, _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();

            _orderIdsToDelete.Add(smsOrder.Id);
            _orderIdsToDelete.Add(emailOrder.Id);

            // month and date are decided by the hardcoded date in the testdata.
            int month = 6;
            int year = 2023;

            // Act
            var actual = await service.GetMonthlyMetrics(month, year);

            // Assert
            Assert.Equal(month, actual.Month);
            Assert.Equal(year, actual.Year);
            Assert.NotEmpty(actual.Metrics);
            Assert.True(actual.Metrics.First(m => m.Org == "ttd").OrdersCreated >= 2);
            Assert.Equal(0, actual.Metrics.First(m => m.Org == "ttd").SmsNotificationsCreated); // smscount column is no longer populated (#1661)
            Assert.True(actual.Metrics.First(m => m.Org == "ttd").EmailNotificationsCreated >= 1);
        }

        [Fact]
        public async Task GetDailySmsMetrics_TwoOrdersPlacedForTTD()
        {
            // Arrange
            MetricsService service = (MetricsService)ServiceUtil
                .GetServices(new List<Type>() { typeof(IMetricsService) })
                .First(i => i.GetType() == typeof(MetricsService));

            (NotificationOrder smsOrder, _) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(resultType: SmsNotificationResultType.Delivered);

            _orderIdsToDelete.Add(smsOrder.Id);

            // Act
            var actual = await service.GetDailySmsMetrics(TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(actual.Metrics);
            Assert.Equal("ttd", actual.Metrics.First().CreatorName);
        }
    }
}
