﻿using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Shared;
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
            if (_orderIdsToDelete.Count != 0)
            {
                string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
                await PostgreUtil.RunSql(deleteSql);
            }
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

        [Fact]
        public async Task SetProcessingStatus_AllStatusesSupported()
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

            foreach (OrderProcessingStatus statusType in Enum.GetValues(typeof(OrderProcessingStatus)))
            {
                // Act
                await repo.SetProcessingStatus(order.Id, statusType);

                // Assert
                string sql = $@"SELECT count(1) 
                                FROM notifications.orders
                                WHERE alternateid = '{order.Id}'
                                AND processedstatus = '{statusType}'";

                int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(sql);

                Assert.Equal(1, orderCount);
            }
        }

        [Fact]
        public async Task CancelOrder_OrderDoesNotExits_ReturnsCancellationError()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            // Act
            Result<NotificationOrderWithStatus, CancellationError> result = await repo.CancelOrder(Guid.NewGuid(), "non-exitent-org");

            // Assert   
            result.Match(
                success =>
                    throw new Exception("No success value should be returned if order is not found in database."),
                error =>
                {
                    Assert.Equal(CancellationError.OrderNotFound, error);
                    return true;
                });
        }

        [Fact]
        public async Task CancelOrder_SendTimePassed_ReturnsError()
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
                RequestedSendTime = DateTime.UtcNow.AddMinutes(-1)
            };

            _orderIdsToDelete.Add(order.Id);
            await repo.Create(order);

            // Act
            Result<NotificationOrderWithStatus, CancellationError> result = await repo.CancelOrder(order.Id, order.Creator.ShortName);

            // Assert   
            result.Match(
                success =>
                    throw new Exception("No success value should be returned if order is not found in database."),
                error =>
                {
                    Assert.Equal(CancellationError.CancellationProhibited, error);
                    return true;
                });
        }

        [Fact]
        public async Task CancelOrder_CancellationConditionSatisfied_ReturnsOrder()
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
                RequestedSendTime = DateTime.UtcNow.AddMinutes(20)
            };

            _orderIdsToDelete.Add(order.Id);
            await repo.Create(order);

            // Act
            Result<NotificationOrderWithStatus, CancellationError> result = await repo.CancelOrder(order.Id, order.Creator.ShortName);

            // Assert   
            result.Match(
                success =>
                {
                    Assert.Equal(OrderProcessingStatus.Cancelled, success.ProcessingStatus.Status);
                    return true;
                },
                error => throw new Exception("No error value should be returned if order satisfies cancellation conditions."));
        }
    }
}
