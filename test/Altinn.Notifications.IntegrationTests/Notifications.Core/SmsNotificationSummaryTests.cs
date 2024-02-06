using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.IntegrationTests.Utils;

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
            if (_orderIdsToDelete.Count != 0)
            {
                string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
                await PostgreUtil.RunSql(deleteSql);
            }
        }

        [Fact]
        public async Task GetSmsSummary_SingleNeweNotification_ReturnsSummary()
        {
            // Arrange
            (NotificationOrder order, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification();
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
                   var actualNotification = actualSummary.Notifications[0];
                   Assert.Equal(SmsNotificationResultType.New, actualNotification.ResultStatus.Result);
                   Assert.NotEmpty(actualNotification.Recipient.MobileNumber);
                   Assert.Equal(notification.Id, actualNotification.Id);
                   Assert.Equivalent(notification.RecipientNumber, actualNotification.Recipient.MobileNumber);
                   return true;
               },
               error =>
               {
                   throw new Exception("Expected a summary, but got an error");
               });
        }

        [Fact]
        public async Task GetSmsSummary_NoOrderIdMatchInDb_ReturnsNull()
        {
            SmsNotificationSummaryService service = (SmsNotificationSummaryService)ServiceUtil
            .GetServices(new List<Type>() { typeof(ISmsNotificationSummaryService) })
            .First(i => i.GetType() == typeof(SmsNotificationSummaryService));

            // Act
            Result<SmsNotificationSummary, ServiceError> result = await service.GetSmsSummary(Guid.NewGuid(), "ttd");

            // Assert
            result.Match(
                success => throw new Exception("No success value should be returned if db returns null"),
                actuallError =>
                {
                    Assert.IsType<ServiceError>(actuallError);
                    Assert.Equal(404, actuallError.ErrorCode);
                    return true;
                });
        }

    }
}
