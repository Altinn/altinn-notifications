using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.AspNetCore.Http.HttpResults;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Core
{
    public class SmsNotificationSummaryTests : IAsyncLifetime
    {
        private readonly List<Guid> _orderIdsToDelete;

        public SmsNotificationSummaryTests()
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
        public async Task GetSmsSummary_SingleNeweNotification_ReturnsSummary()
        {
            // Arrange
            (NotificationOrder order, _) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
            _orderIdsToDelete.Add(order.Id);

            SmsNotificationSummaryService service = (SmsNotificationSummaryService)ServiceUtil
             .GetServices(new List<Type>() { typeof(ISmsNotificationSummaryService) })
             .First(i => i.GetType() == typeof(SmsNotificationSummaryService));

            // Act
            Result<SmsNotificationSummary, ServiceError> result = await service.GetSmsSummary(order.Id, "ttd");

            // Assert
            result.Match(
               actualSummary =>
               {
                   Assert.Single(actualSummary.Notifications);
                   var notification = actualSummary.Notifications[0];
                   Assert.Equal(SmsNotificationResultType.New, notification.ResultStatus.Result);
                   return true;
               },
               error =>
               {
                   throw new Exception("Expected a summary, but got an error");
               });
        }
    }
}
