using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
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

        [Fact]
        public async Task Create_OrderChainWithMainNotification_SuccessfullyPersistsMainOrder()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices([typeof(IOrderRepository)])
                .First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            _orderIdsToDelete.Add(orderId);

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
             .SetOrderId(orderId)
             .SetCreator(new Creator("skd"))
             .SetRequestedSendTime(DateTime.UtcNow.AddMinutes(10))
             .SetIdempotencyId("5D69E05E-8BC7-4736-BADA-C6CB00ED8C0A")
             .SetSendersReference("D340DC99-E5B0-4153-B56E-B3946E8D4AC4")
             .SetRecipient(new NotificationRecipient
             {
                 RecipientEmail = new RecipientEmail
                 {
                     EmailAddress = "recipient@example.com",
                     Settings = new EmailSendingOptions
                     {
                         Body = "Test body",
                         Subject = "Test subject",
                         ContentType = EmailContentType.Plain
                     }
                 }
             })
             .Build();

            NotificationOrder mainOrder = new()
            {
                Id = orderId,
                Creator = new("skd"),
                Created = DateTime.UtcNow,
                SendersReference = "D340DC99-E5B0-4153-B56E-B3946E8D4AC4",
                Templates =
                [
                    new EmailTemplate("noreply@altinn.no", "Test subject", "Test body", EmailContentType.Plain)
                ]
            };

            // Act
            var result = await repo.Create(orderRequest, mainOrder, null);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(orderId, result[0].Id);

            string emailSql = $@"SELECT count(1) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{orderId}'";

            int emailTextCount = await PostgreUtil.RunSqlReturnOutput<int>(emailSql);
            Assert.Equal(1, emailTextCount);

            string orderChainSql = $@"SELECT count(1) FROM notifications.orderschain WHERE orderid = '{orderId}'";

            int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
            Assert.Equal(1, orderChainCount);
        }

        [Fact]
        public async Task Create_OrderChainWithMainNotificationAndReminders_SuccessfullyPersistsAllOrders()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            Guid mainOrderId = Guid.NewGuid();
            Guid reminderOrderId1 = Guid.NewGuid();
            Guid reminderOrderId2 = Guid.NewGuid();

            _orderIdsToDelete.Add(mainOrderId);
            _orderIdsToDelete.Add(reminderOrderId1);
            _orderIdsToDelete.Add(reminderOrderId2);

            var recipient = new NotificationRecipient
            {
                RecipientEmail = new RecipientEmail
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptions
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                        ContentType = EmailContentType.Plain
                    }
                }
            };

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(mainOrderId)
                .SetCreator(new Creator("test"))
                .SetIdempotencyId("test-idempotency-id-with-reminders")
                .SetRecipient(recipient)
                .SetReminders(new List<NotificationReminder>
                {
            new NotificationReminder
            {
                OrderId = reminderOrderId1,
                DelayDays = 3,
                Recipient = recipient
            },
            new NotificationReminder
            {
                OrderId = reminderOrderId2,
                DelayDays = 7,
                Recipient = recipient
            }
                })
                .Build();

            NotificationOrder mainOrder = new()
            {
                Id = mainOrderId,
                Created = DateTime.UtcNow,
                Creator = new("test"),
                Templates = new List<INotificationTemplate>()
        {
            new EmailTemplate("noreply@altinn.no", "Main Subject", "Main Body", EmailContentType.Plain)
        }
            };

            List<NotificationOrder> reminders = new()
    {
        new NotificationOrder
        {
            Id = reminderOrderId1,
            Created = DateTime.UtcNow,
            Creator = new("test"),
            Templates = new List<INotificationTemplate>()
            {
                new EmailTemplate("noreply@altinn.no", "Reminder 1 Subject", "Reminder 1 Body", EmailContentType.Plain)
            }
        },
        new NotificationOrder
        {
            Id = reminderOrderId2,
            Created = DateTime.UtcNow,
            Creator = new("test"),
            Templates = new List<INotificationTemplate>()
            {
                new EmailTemplate("noreply@altinn.no", "Reminder 2 Subject", "Reminder 2 Body", EmailContentType.Plain)
            }
        }
    };

            // Act
            var result = await repo.Create(orderRequest, mainOrder, reminders);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(mainOrderId, result[0].Id);
            Assert.Equal(reminderOrderId1, result[1].Id);
            Assert.Equal(reminderOrderId2, result[2].Id);

            string mainOrderSql = $@"SELECT count(1) 
      FROM notifications.orders
      WHERE alternateid = '{mainOrderId}'";

            string reminder1Sql = $@"SELECT count(1) 
      FROM notifications.orders
      WHERE alternateid = '{reminderOrderId1}'";

            string reminder2Sql = $@"SELECT count(1) 
      FROM notifications.orders
      WHERE alternateid = '{reminderOrderId2}'";

            string ordersChainSql = $@"SELECT count(1) 
      FROM notifications.orderschain
      WHERE orderid = '{mainOrderId}'";

            int mainOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrderSql);
            int reminder1Count = await PostgreUtil.RunSqlReturnOutput<int>(reminder1Sql);
            int reminder2Count = await PostgreUtil.RunSqlReturnOutput<int>(reminder2Sql);
            int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(ordersChainSql);

            Assert.Equal(1, mainOrderCount);
            Assert.Equal(1, reminder1Count);
            Assert.Equal(1, reminder2Count);
            Assert.Equal(1, orderChainCount);
        }

        [Fact]
        public async Task Create_OrderChainWithMixedTemplates_AllTemplateTypesPersisted()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            Guid mainOrderId = Guid.NewGuid();
            Guid reminderOrderId = Guid.NewGuid();

            _orderIdsToDelete.Add(mainOrderId);
            _orderIdsToDelete.Add(reminderOrderId);

            var recipient = new NotificationRecipient
            {
                RecipientEmail = new RecipientEmail
                {
                    EmailAddress = "recipient@example.com",
                    Settings = new EmailSendingOptions
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                        ContentType = EmailContentType.Plain
                    }
                }
            };

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(mainOrderId)
                .SetCreator(new Creator("test"))
                .SetIdempotencyId("test-idempotency-id-mixed")
                .SetRecipient(recipient)
                .SetReminders(new List<NotificationReminder>
                {
            new NotificationReminder
            {
                OrderId = reminderOrderId,
                DelayDays = 3,
                Recipient = recipient
            }
                })
                .Build();

            NotificationOrder mainOrder = new()
            {
                Id = mainOrderId,
                Created = DateTime.UtcNow,
                Creator = new("test"),
                Templates = new List<INotificationTemplate>()
        {
            new EmailTemplate("noreply@altinn.no", "Email Subject", "Email Body", EmailContentType.Plain)
        }
            };

            List<NotificationOrder> reminders = new()
    {
        new NotificationOrder
        {
            Id = reminderOrderId,
            Created = DateTime.UtcNow,
            Creator = new("test"),
            Templates = new List<INotificationTemplate>()
            {
                new SmsTemplate("Altinn", "This is the SMS reminder")
            }
        }
    };

            // Act
            var result = await repo.Create(orderRequest, mainOrder, reminders);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            string mainEmailSql = $@"SELECT count(1) 
      FROM notifications.emailtexts as et
      JOIN notifications.orders o ON et._orderid = o._id
      WHERE o.alternateid = '{mainOrderId}'";

            string reminderSmsSql = $@"SELECT count(1) 
      FROM notifications.smstexts as st
      JOIN notifications.orders o ON st._orderid = o._id
      WHERE o.alternateid = '{reminderOrderId}'";

            int mainEmailCount = await PostgreUtil.RunSqlReturnOutput<int>(mainEmailSql);
            int reminderSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(reminderSmsSql);

            Assert.Equal(1, mainEmailCount);
            Assert.Equal(1, reminderSmsCount);
        }

        [Fact]
        public async Task Create_OrderChainWithCustomSendTime_SendTimeCorrectlyPersisted()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            _orderIdsToDelete.Add(orderId);

            DateTime futureTime = DateTime.UtcNow.AddDays(2);

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetCreator(new Creator("test"))
                .SetIdempotencyId("test-idempotency-id-future")
                .SetRequestedSendTime(futureTime)
                .Build();

            NotificationOrder mainOrder = new()
            {
                Id = orderId,
                Created = DateTime.UtcNow,
                Creator = new("test"),
                RequestedSendTime = futureTime,
                Templates = new List<INotificationTemplate>()
        {
            new EmailTemplate("noreply@altinn.no", "Future Subject", "Future Body", EmailContentType.Plain)
        }
            };

            // Act
            var result = await repo.Create(orderRequest, mainOrder, null);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);

            string sendTimeSql = $@"SELECT requestedsendtime 
      FROM notifications.orders
      WHERE alternateid = '{orderId}'";

            DateTime persistedSendTime = await PostgreUtil.RunSqlReturnOutput<DateTime>(sendTimeSql);

            // Compare dates with a small tolerance for millisecond differences
            Assert.True((persistedSendTime - futureTime).Duration().TotalSeconds < 1, $"Expected {futureTime}, but got {persistedSendTime}");
        }

        [Fact]
        public async Task Create_OrderChainWithConditionEndpoint_EndpointCorrectlyPersisted()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil
                .GetServices(new List<Type>() { typeof(IOrderRepository) })
                .First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            _orderIdsToDelete.Add(orderId);

            Uri conditionEndpoint = new Uri("https://example.com/condition");

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetCreator(new Creator("test"))
                .SetIdempotencyId("test-idempotency-id-condition")
                .SetConditionEndpoint(conditionEndpoint)
                .Build();

            NotificationOrder mainOrder = new()
            {
                Id = orderId,
                Created = DateTime.UtcNow,
                Creator = new("test"),
                ConditionEndpoint = conditionEndpoint,
                Templates = new List<INotificationTemplate>()
        {
            new EmailTemplate("noreply@altinn.no", "Conditional Subject", "Conditional Body", EmailContentType.Plain)
        }
            };

            // Act
            var result = await repo.Create(orderRequest, mainOrder, null);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);

            var retrievedOrder = await repo.GetOrderWithStatusById(orderId, "test");
            Assert.Equal(conditionEndpoint.ToString(), retrievedOrder?.ConditionEndpoint?.ToString());
        }
    }
}
