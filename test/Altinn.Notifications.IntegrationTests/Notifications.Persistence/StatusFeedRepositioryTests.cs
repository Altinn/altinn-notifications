using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    public class StatusFeedRepositioryTests : IAsyncLifetime
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
        public async Task ReadStatusFeed()
        {
            // Arrange
            var sqlInsert = $@"INSERT INTO notifications.statusfeed(
                              sequencenumber, orderid, creatorname, created, orderstatus)
                              VALUES(1, 123, '{_creatorName}', '2025-05-21', '{{""validjson"":""true""}}')";

            await PostgreUtil.RunSql(sqlInsert);

            StatusFeedRepository statusFeedRepository = (StatusFeedRepository)ServiceUtil
                .GetServices([typeof(IStatusFeedRepository)])
                .First(i => i.GetType() == typeof(StatusFeedRepository));

            // Act
            var results = await statusFeedRepository.GetStatusFeed(1, _creatorName, CancellationToken.None);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Count > 0);
            Assert.Equal(1, results[0].SequenceNumber);
        }
    }
}
