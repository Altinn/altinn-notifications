using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTests.Utils;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    public class MetricsServiceTests : IAsyncLifetime
    {
        private readonly List<Guid> _orderIdsToDelete;

        public MetricsServiceTests()
        {
            _orderIdsToDelete = new List<Guid>();
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
            await PostgreUtil.RunSql(deleteSql);
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
            Assert.True(actual.Metrics.First(m => m.Org == "ttd").SmsNotificationsCreated >= 1);
            Assert.True(actual.Metrics.First(m => m.Org == "ttd").EmailNotificationsCreated >= 1);
        }
    }
}
