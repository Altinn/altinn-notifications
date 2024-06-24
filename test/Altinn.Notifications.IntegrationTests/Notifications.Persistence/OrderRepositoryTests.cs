using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    public class OrderRepositoryTests : IAsyncLifetime
    {
        private readonly List<Guid> _orderIdsToDelete;

        public OrderRepositoryTests()
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
        public async Task Create_OrderWithSmsTemplate_SmsTextsPersisted()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            NotificationOrder order = new()
            {
                Id = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                Creator = new("test"),
                Templates = new List<INotificationTemplate>()
                {
                    new SmsTemplate("Altinn", "This is the body")
                },
                RequestedSendTime = DateTime.UtcNow
            };

            _orderIdsToDelete.Add(order.Id);

            // Act
            await repo.Create(order);

            // Assert
            string sql = $@"SELECT count(1) 
              FROM notifications.smstexts as st
              JOIN notifications.orders o ON st._orderid = o._id
              WHERE o.alternateid = '{order.Id}'";

            int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

            Assert.Equal(1, actualCount);
        }

        [Fact]
        public async Task Create_OrderWithEmailTemplate_EmailTextsPersisted()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            NotificationOrder order = new()
            {
                Id = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                Creator = new("test"),
                Templates = new List<INotificationTemplate>()
                {
                    new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain)
                }
            };

            _orderIdsToDelete.Add(order.Id);

            // Act
            await repo.Create(order);

            // Assert
            string sql = $@"SELECT count(1) 
              FROM notifications.emailtexts as et
              JOIN notifications.orders o ON et._orderid = o._id
              WHERE o.alternateid = '{order.Id}'";

            int actualCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

            Assert.Equal(1, actualCount);
        }

        [Fact]
        public async Task Create_OrderWithEmailAndSmsTemplate_BothTextsPersisted()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            NotificationOrder order = new()
            {
                Id = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                Creator = new("test"),
                Templates = new List<INotificationTemplate>()
                {
                    new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain),
                    new SmsTemplate("Altinn", "This is the body")
                }
            };

            _orderIdsToDelete.Add(order.Id);

            // Act
            await repo.Create(order);

            // Assert
            string emailSql = $@"SELECT count(1) 
              FROM notifications.emailtexts as et
              JOIN notifications.orders o ON et._orderid = o._id
              WHERE o.alternateid = '{order.Id}'";

            string smsSql = $@"SELECT count(1) 
              FROM notifications.smstexts as st
              JOIN notifications.orders o ON st._orderid = o._id
              WHERE o.alternateid = '{order.Id}'";

            int emailTextCount = await PostgreUtil.RunSqlReturnOutput<int>(emailSql);
            int smsTextCound = await PostgreUtil.RunSqlReturnOutput<int>(smsSql);

            Assert.Equal(1, emailTextCount);
            Assert.Equal(1, smsTextCound);
        }

        [Fact]
        public async Task GetOrderWithStatusById_ConfirmConditionEndpoint()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            NotificationOrder order = new()
            {
                Id = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                Creator = new("test"),
                Templates = new List<INotificationTemplate>()
                {
                    new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain),
                    new SmsTemplate("Altinn", "This is the body")
                },
                ConditionEndpoint = new Uri("https://vg.no/condition")
            };

            _orderIdsToDelete.Add(order.Id);
            await repo.Create(order);

            // Act
            NotificationOrderWithStatus? actual = await repo.GetOrderWithStatusById(order.Id, "test");

            // Assert
            Assert.Equal("https://vg.no/condition", actual?.ConditionEndpoint?.ToString());
        }
    }
}
