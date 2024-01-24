using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence
{
    public class OrderRepositoryTests
    {
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
                }
            };

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
                    new EmailTemplate("noreply@altinn.no", "Subject", "Body", Core.Enums.EmailContentType.Plain)
                }
            };

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
    }
}
