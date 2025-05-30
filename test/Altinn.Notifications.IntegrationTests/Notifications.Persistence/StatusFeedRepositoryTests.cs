using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    public class StatusFeedRepositoryTests : IAsyncLifetime
    {
        private readonly string _creatorName = "testcase";

        public async Task DisposeAsync()
        {
            string deleteSql = $@"DELETE from notifications.statusfeed s where s.creatorname = '{_creatorName}'";
            await PostgreUtil.RunSql(deleteSql);
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task ReadStatusFeedWithTestCreatorName()
        {
            // Arrange
            var orderStatusFeedTestOrderCompleted = @"{
              ""Status"": ""Order_Completed"",
              ""Recipients"": [
                {
                  ""Type"": ""Email"",
                  ""Status"": ""Email_Delivered"",
                  ""Destination"": ""navn.navnesen@example.com""
                },
                {
                  ""Type"": ""SMS"",
                  ""Status"": ""SMS_Delivered"",
                  ""Destination"": ""+4799999999""
                }
              ],
              ""ShipmentId"": ""f5d51690-87c8-4df8-a980-15f4554337e8"",
              ""LastUpdated"": ""2025-03-28T16:24:17.8182889+01:00"",
              ""ShipmentType"": ""Notification"",
              ""SendersReference"": ""Random-Senders-Reference-55027""
            }";

            var sqlInsert = $@"INSERT INTO notifications.statusfeed(
                              orderid, creatorname, created, orderstatus)
                              VALUES(123, '{_creatorName}', '2025-05-21', '{orderStatusFeedTestOrderCompleted}')";

            await PostgreUtil.RunSql(sqlInsert);

            StatusFeedRepository statusFeedRepository = (StatusFeedRepository)ServiceUtil
                .GetServices([typeof(IStatusFeedRepository)])
                .First(i => i.GetType() == typeof(StatusFeedRepository));

            // Act
            var results = await statusFeedRepository.GetStatusFeed(0, _creatorName, CancellationToken.None);

            // Assert
            var item = Assert.Single(results);
            Assert.True(item.SequenceNumber > 0);
            Assert.Equal(Altinn.Notifications.Core.Enums.ProcessingLifecycle.Order_Completed, item.OrderStatus.Status);
        }

        [Fact]
        public async Task ReadStatusFeedWithEmptyCreatorName()
        {
            // Arrange
            StatusFeedRepository statusFeedRepository = (StatusFeedRepository)ServiceUtil
                .GetServices([typeof(IStatusFeedRepository)])
                .First(i => i.GetType() == typeof(StatusFeedRepository));
            
            // Act
            var results = await statusFeedRepository.GetStatusFeed(1, string.Empty, CancellationToken.None);
            
            // Assert
            Assert.Empty(results);
        }
    }
}
