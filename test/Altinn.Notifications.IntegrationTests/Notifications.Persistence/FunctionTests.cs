using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    public class FunctionTests
    {
        private readonly int _publishBatchSize = 500;

        /// <summary>
        /// Scenario: Registered email limit timeout in db has passed
        /// Expected side effect: Value is reset to NULL when getemails_statusnew_updatestatus is called by <see cref="EmailNotificationRepository"/>    
        /// </summary>
        [Fact]
        public async Task Run_getemails_statusnew_updatestatus_ConfirmSideEffects()
        {
            // Arrange
            string sql = @"UPDATE notifications.resourcelimitlog
                        SET emaillimittimeout = '2023-06-16 18:32:29.175852+01'
                        WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";
            await PostgreUtil.RunSql(sql);

            // Act
            var serviceList = ServiceUtil.GetServices(new List<Type>() { typeof(IEmailNotificationRepository) });
            EmailNotificationRepository repository = (EmailNotificationRepository)serviceList.First(i => i.GetType() == typeof(EmailNotificationRepository));

            await repository.GetNewNotificationsAsync(_publishBatchSize, CancellationToken.None);

            // Assert
            sql = @"SELECT emaillimittimeout
	                   FROM notifications.resourcelimitlog
	                   order by id desc
	                   limit 1;";

            DateTime? actualTimeout = await PostgreUtil.RunSqlReturnOutput<DateTime?>(sql);
            Assert.Null(actualTimeout);
        }
    }
}
