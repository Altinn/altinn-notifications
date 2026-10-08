using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    [Collection(GlobalStateSerialCollection.Name)]
    public class FunctionTests : IAsyncLifetime
    {
        public ValueTask InitializeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            // Reset the email timeout to NULL so a failed run does not leave global state behind
            string cleanupSql = @"UPDATE notifications.resourcelimitlog
                                  SET emaillimittimeout = NULL
                                  WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";
            await PostgreUtil.RunSql(cleanupSql);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Scenario: Registered email limit timeout in db has passed
        /// Expected side effect: Value is reset to NULL when GetNewNotificationAsync is called by <see cref="EmailNotificationRepository"/>.
        /// </summary>
        [Fact]
        public async Task GetNewNotificationAsync_WhenEmailLimitTimeoutHasExpired_ResetsTimeoutToNull()
        {
            // Arrange
            string sql = @"UPDATE notifications.resourcelimitlog
                        SET emaillimittimeout = '2023-06-16 18:32:29.175852+01'
                        WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";
            await PostgreUtil.RunSql(sql);

            // Act
            var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IEmailNotificationRepository) });
            EmailNotificationRepository repository = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

            await using NpgsqlConnection connection = await ServiceUtil.GetSharedDataSource().OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var unitOfWork = new UnitOfWork
            {
                Connection = connection,
                Transaction = transaction
            };

            await repository.GetNewNotificationAsync(unitOfWork, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);

            // Assert
            sql = @"SELECT emaillimittimeout
	                   FROM notifications.resourcelimitlog
	                   order by id desc
	                   limit 1;";

            DateTime? actualTimeout = await PostgreUtil.RunSqlReturnOutput<DateTime?>(sql);
            Assert.Null(actualTimeout);
        }

        /// <summary>
        /// Scenario: Registered email limit timeout in db is still in the future.
        /// Expected behavior: GetNewNotificationAsync returns null and does not modify emaillimittimeout.
        /// </summary>
        [Fact]
        public async Task GetNewNotificationAsync_WhenEmailLimitTimeoutIsInFuture_ReturnsNullAndPreservesTimeout()
        {
            // Arrange
            string updateSql = @"UPDATE notifications.resourcelimitlog
                                SET emaillimittimeout = NOW() + INTERVAL '30 minutes'
                                WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";
            await PostgreUtil.RunSql(updateSql);

            const string selectSql = @"SELECT emaillimittimeout
                                       FROM notifications.resourcelimitlog
                                       ORDER BY id DESC
                                       LIMIT 1;";

            DateTime? timeoutBeforeClaim = await PostgreUtil.RunSqlReturnOutput<DateTime?>(selectSql);
            Assert.NotNull(timeoutBeforeClaim);

            // Act
            var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IEmailNotificationRepository) });
            EmailNotificationRepository repository = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

            await using NpgsqlConnection connection = await ServiceUtil.GetSharedDataSource().OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var unitOfWork = new UnitOfWork
            {
                Connection = connection,
                Transaction = transaction
            };

            Email? claimedNotification = await repository.GetNewNotificationAsync(unitOfWork, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(claimedNotification);

            DateTime? timeoutAfterClaim = await PostgreUtil.RunSqlReturnOutput<DateTime?>(selectSql);
            Assert.Equal(timeoutBeforeClaim, timeoutAfterClaim);
        }
    }
}
