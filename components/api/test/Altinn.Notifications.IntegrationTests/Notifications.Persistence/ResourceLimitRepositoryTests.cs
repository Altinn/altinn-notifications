using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class ResourceLimitRepositoryTests : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up: Reset the email timeout to NULL after tests
        string cleanupSql = @"UPDATE notifications.resourcelimitlog
                              SET emaillimittimeout = NULL
                              WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";
        await PostgreUtil.RunSql(cleanupSql);
    }

    [Fact]
    public async Task SetEmailTimeout_WhenRowExists_ShouldUpdateExistingRow()
    {
        // Arrange
        ResourceLimitRepository sut = (ResourceLimitRepository)ServiceUtil
            .GetServices([typeof(IResourceLimitRepository)])
            .First(i => i.GetType() == typeof(ResourceLimitRepository));

        DateTime initialTimeout = DateTime.UtcNow.AddMinutes(5);
        DateTime newTimeout = DateTime.UtcNow.AddMinutes(10);

        // Ensure there's an existing row with a timeout value
        string setupSql = @"UPDATE notifications.resourcelimitlog
                           SET emaillimittimeout = @timeout
                           WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";
        await PostgreUtil.RunSql(setupSql, new Npgsql.NpgsqlParameter("@timeout", initialTimeout));

        // Verify initial state
        DateTime? initialValue = await GetEmailTimeoutFromDb();
        Assert.NotNull(initialValue);

        // Act
        bool result = await sut.SetEmailTimeout(newTimeout);

        // Assert
        Assert.True(result);
        
        DateTime? actualTimeout = await GetEmailTimeoutFromDb();
        Assert.NotNull(actualTimeout);
        Assert.Equal(
            newTimeout.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"), 
            actualTimeout.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        
        // Verify only one row was affected (no new rows inserted)
        int rowCount = await GetResourceLimitLogRowCount();
        Assert.Equal(1, rowCount); // Should still be just one row
    }

    [Fact]
    public async Task SetEmailTimeout_WhenNoRowExists_ShouldInsertNewRow()
    {
        // Arrange
        ResourceLimitRepository sut = (ResourceLimitRepository)ServiceUtil
            .GetServices([typeof(IResourceLimitRepository)])
            .First(i => i.GetType() == typeof(ResourceLimitRepository));

        DateTime timeout = DateTime.UtcNow.AddMinutes(15);

        // Ensure the table is empty or has no rows with timeout
        string deleteSql = @"DELETE FROM notifications.resourcelimitlog";
        await PostgreUtil.RunSql(deleteSql);

        // Act
        bool result = await sut.SetEmailTimeout(timeout);

        // Assert
        Assert.True(result);
        
        DateTime? actualTimeout = await GetEmailTimeoutFromDb();
        Assert.NotNull(actualTimeout);
        Assert.Equal(
            timeout.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"), 
            actualTimeout.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
    }

    [Fact]
    public async Task SetEmailTimeout_WhenRowExistsWithNullValue_ShouldUpdateToNewValue()
    {
        // Arrange
        ResourceLimitRepository sut = (ResourceLimitRepository)ServiceUtil
            .GetServices([typeof(IResourceLimitRepository)])
            .First(i => i.GetType() == typeof(ResourceLimitRepository));

        DateTime timeout = DateTime.UtcNow.AddMinutes(20);

        // Ensure there's a row with NULL timeout
        string setupSql = @"UPDATE notifications.resourcelimitlog
                           SET emaillimittimeout = NULL
                           WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";
        await PostgreUtil.RunSql(setupSql);

        // Verify initial state
        DateTime? initialValue = await GetEmailTimeoutFromDb();
        Assert.Null(initialValue);

        // Act
        bool result = await sut.SetEmailTimeout(timeout);

        // Assert
        Assert.True(result);
        
        DateTime? actualTimeout = await GetEmailTimeoutFromDb();
        Assert.NotNull(actualTimeout);
        Assert.Equal(
            timeout.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"), 
            actualTimeout.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private static async Task<DateTime?> GetEmailTimeoutFromDb()
    {
        string sql = @"SELECT emaillimittimeout
                       FROM notifications.resourcelimitlog
                       ORDER BY id DESC
                       LIMIT 1;";

        return await PostgreUtil.RunSqlReturnOutput<DateTime?>(sql);
    }

    private static async Task<int> GetResourceLimitLogRowCount()
    {
        string sql = @"SELECT COUNT(*) FROM notifications.resourcelimitlog;";
        return await PostgreUtil.RunSqlReturnOutput<int>(sql);
    }
}
