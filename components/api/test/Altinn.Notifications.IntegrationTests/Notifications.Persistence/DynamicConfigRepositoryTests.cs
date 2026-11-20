using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class DynamicConfigRepositoryTests
{
    [Fact]
    public async Task GetPodCount_RefreshesCurrentPodAndRemovesExpiredPods()
    {
        Guid currentPod = Guid.NewGuid();
        Guid expiredPod = Guid.NewGuid();
        var repository = new DynamicConfigRepository(ServiceUtil.GetSharedDataSource());

        try
        {
            await PostgreUtil.RunSql(
                """
                INSERT INTO notifications.podstate (id, created, lastrefreshed)
                VALUES (@current, now() - interval '1 minute', now() - interval '1 minute'),
                       (@expired, now() - interval '3 minutes', now() - interval '3 minutes')
                """,
                new NpgsqlParameter("current", currentPod),
                new NpgsqlParameter("expired", expiredPod));

            int podCount = await repository.GetPodCount(currentPod, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);

            Assert.True(podCount >= 1);
            Assert.True(await PostgreUtil.RunSqlReturnOutput<bool>(
                "SELECT lastrefreshed > created FROM notifications.podstate WHERE id = @id",
                new NpgsqlParameter("id", currentPod)));
            Assert.False(await PostgreUtil.RunSqlReturnOutput<bool>(
                "SELECT EXISTS (SELECT FROM notifications.podstate WHERE id = @id)",
                new NpgsqlParameter("id", expiredPod)));
        }
        finally
        {
            await PostgreUtil.RunSql(
                "DELETE FROM notifications.podstate WHERE id IN (@current, @expired)",
                new NpgsqlParameter("current", currentPod),
                new NpgsqlParameter("expired", expiredPod));
        }
    }

    [Fact]
    public async Task GetConfig_ReturnsStoredValuesWithCaseInsensitiveKeys()
    {
        string key = $"IntegrationTests:{Guid.NewGuid():N}:TaskCount";
        var repository = new DynamicConfigRepository(ServiceUtil.GetSharedDataSource());
        NpgsqlDataSource adminDataSource = ServiceUtil.GetSharedAdminDataSource();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var insert = adminDataSource.CreateCommand("INSERT INTO notifications.config (key, value) VALUES (@key, '24')");
        insert.Parameters.AddWithValue("key", key);
        await insert.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            IReadOnlyDictionary<string, string> config = await repository.GetConfig(cancellationToken);

            Assert.Equal("24", config[key.ToLowerInvariant()]);
        }
        finally
        {
            await using var delete = adminDataSource.CreateCommand("DELETE FROM notifications.config WHERE key = @key");
            delete.Parameters.AddWithValue("key", key);
            await delete.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
