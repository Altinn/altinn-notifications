using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
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
        private readonly List<Guid> _ordersChainIdsToDelete;

        public OrderRepositoryTests()
        {
            _orderIdsToDelete = [];
            _ordersChainIdsToDelete = [];
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

            if (_ordersChainIdsToDelete.Count != 0)
            {
                string deleteSql = $@"DELETE from notifications.orderschain oc where oc.orderid in ('{string.Join("','", _ordersChainIdsToDelete)}')";
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
        public async Task Create_NotificationOrderChain_WithEmailRecipient_NoReminders_VerifiesDatabasePersistence()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            // Track identifiers for cleanup.
            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetOrderChainId(orderChainId)
                .SetCreator(new Creator("skd"))
                .SetType(OrderType.Notification)
                .SetRequestedSendTime(requestedSendTime)
                .SetConditionEndpoint(new Uri("https://vg.no/condition"))
                .SetIdempotencyId("EBEF8B94-3F8C-444E-BF94-6F6B1FA0417C")
                .SetSendersReference("D340DC99-E5B0-4153-B56E-B3946E8D4AC4")
                .SetRecipient(new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = "recipient@example.com",
                        Settings = new EmailSendingOptions
                        {
                            Body = "Email body",
                            Subject = "Email subject",
                            SenderEmailAddress = "Email sender address",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime
                        }
                    }
                })
                .Build();

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new("skd"),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Email,
                ConditionEndpoint = new Uri("https://vg.no/condition"),
                SendersReference = "D340DC99-E5B0-4153-B56E-B3946E8D4AC4",
                Templates =
                [
                    new EmailTemplate("Email sender address", "Email subject", "Email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([new EmailAddressPoint("recipient@example.com")])
                ]
            };

            // Act
            var result = await repo.Create(orderChainRequest, notificationOrder, null);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(orderId, result[0].Id);
            Assert.NotEqual(orderChainId, result[0].Id);

            string orderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
            Assert.Equal(1, orderChainCount);

            string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}' and type ='Notification'";
            int ordersCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
            Assert.Equal(1, ordersCount);

            string emailTextSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int emailTextCount = await PostgreUtil.RunSqlReturnOutput<int>(emailTextSql);
            Assert.Equal(1, emailTextCount);
        }

        [Fact]
        public async Task Create_NotificationOrderChain_WithSmsRecipient_WithReminders_VerifiesDatabasePersistence()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid mainOrderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();
            Guid firstReminderOrderId = Guid.NewGuid();
            Guid secondReminderOrderId = Guid.NewGuid();

            _ordersChainIdsToDelete.AddRange(orderChainId);
            _orderIdsToDelete.AddRange([mainOrderId, firstReminderOrderId, secondReminderOrderId]);

            var creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(mainOrderId)
                .SetOrderChainId(orderChainId)
                .SetCreator(new Creator("ttd"))
                .SetType(OrderType.Notification)
                .SetSendersReference("ref-D3C9BA54")
                .SetRequestedSendTime(requestedSendTime)
                .SetIdempotencyId("F1E2D3C4-B5A6-9876-5432-1098ABCDEF01")
                .SetRecipient(new NotificationRecipient
                {
                    RecipientSms = new RecipientSms
                    {
                        PhoneNumber = "+4799999999",
                        Settings = new SmsSendingOptions
                        {
                            Body = "Main order SMS body",
                            Sender = "Main order SMS sender",
                            SendingTimePolicy = SendingTimePolicy.Daytime
                        }
                    }
                })
                .SetReminders(
                [
                    new NotificationReminder
                    {
                        DelayDays = 3,
                        Type = OrderType.Reminder,
                        OrderId = firstReminderOrderId,
                        SendersReference = "ref-reminder-A3BCFE4284D6",
                        RequestedSendTime = requestedSendTime.AddDays(3),
                        ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),
                        Recipient = new NotificationRecipient
                        {
                            RecipientSms = new RecipientSms
                            {
                                PhoneNumber = "+4799999999",
                                Settings = new SmsSendingOptions
                                {
                                    Body = "First reminder SMS body",
                                    Sender = "First reminder SMS sender",
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                }
                            }
                        }
                    },
                    new NotificationReminder
                    {
                        DelayDays = 7,
                        Type = OrderType.Reminder,
                        OrderId = secondReminderOrderId,
                        SendersReference = "ref-reminder-F2491E785C2D",
                        RequestedSendTime = requestedSendTime.AddDays(7),
                        ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),
                        Recipient = new NotificationRecipient
                        {
                            RecipientSms = new RecipientSms
                            {
                                PhoneNumber = "+4799999999",
                                Settings = new SmsSendingOptions
                                {
                                    Body = "Second reminder SMS body",
                                    Sender = "Second reminder SMS sender",
                                    SendingTimePolicy = SendingTimePolicy.Daytime
                                }
                            }
                        }
                    }
                ])
                .Build();

            NotificationOrder mainOrder = new()
            {
                Id = mainOrderId,
                Creator = new("ttd"),
                Created = creationDateTime,
                Type = OrderType.Notification,
                SendersReference = "ref-D3C9BA54",
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Sms,
                Templates =
                [
                    new SmsTemplate("Main order SMS sender", "Main order SMS body")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            List<NotificationOrder> reminders =
            [
                new NotificationOrder
                {
                    Creator = new("ttd"),
                    Id = firstReminderOrderId,
                    Created = creationDateTime,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestedSendTime.AddDays(3),
                    SendersReference = "ref-reminder-A3BCFE4284D6",
                    NotificationChannel = NotificationChannel.Sms,
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),
                    Templates =
                    [
                        new SmsTemplate("First reminder SMS sender", "First reminder SMS body")
                    ],
                    Recipients =
                    [
                        new Recipient([new SmsAddressPoint("+4799999999")])
                    ]
                },
                new NotificationOrder
                {
                    Creator = new("ttd"),
                    Id = secondReminderOrderId,
                    Created = creationDateTime,
                    Type = OrderType.Reminder,
                    NotificationChannel = NotificationChannel.Sms,
                    SendersReference = "ref-reminder-F2491E785C2D",
                    RequestedSendTime = requestedSendTime.AddDays(7),
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),
                    Templates =
                    [
                        new SmsTemplate("Second reminder SMS sender", "Second reminder SMS body")
                    ],
                    Recipients =
                    [
                        new Recipient([new SmsAddressPoint("+4799999999")])
                    ]
                }
            ];

            // Act
            var result = await repo.Create(orderRequest, mainOrder, reminders);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(mainOrderId, result[0].Id);
            Assert.NotEqual(orderChainId, result[0].Id);
            Assert.NotEqual(orderChainId, result[1].Id);
            Assert.NotEqual(orderChainId, result[2].Id);
            Assert.Equal(firstReminderOrderId, result[1].Id);
            Assert.Equal(secondReminderOrderId, result[2].Id);

            string mainOrdersChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            string mainOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{mainOrderId}' and type ='Notification'";
            string firstReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstReminderOrderId}' and type ='Reminder'";
            string secondReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondReminderOrderId}' and type ='Reminder'";
            string firstSmsTextSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{mainOrderId}'";
            string secondSmsTextSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{mainOrderId}'";

            int mainOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrderSql);
            int firstSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(firstSmsTextSql);
            int secondSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(secondSmsTextSql);
            int firstReminderCount = await PostgreUtil.RunSqlReturnOutput<int>(firstReminderSql);
            int secondReminderCount = await PostgreUtil.RunSqlReturnOutput<int>(secondReminderSql);
            int mainOrdersChainCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrdersChainSql);

            Assert.Equal(1, firstSmsCount);
            Assert.Equal(1, secondSmsCount);
            Assert.Equal(1, mainOrderCount);
            Assert.Equal(1, firstReminderCount);
            Assert.Equal(1, secondReminderCount);
            Assert.Equal(1, mainOrdersChainCount);
        }

        [Fact]
        public async Task Create_NotificationOrderChain_WithPersonRecipient_WithReminders_VerifiesDatabasePersistence()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid mainOrderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();
            Guid firstReminderOrderId = Guid.NewGuid();
            Guid secondReminderOrderId = Guid.NewGuid();

            _ordersChainIdsToDelete.AddRange(orderChainId);
            _orderIdsToDelete.AddRange([mainOrderId, firstReminderOrderId, secondReminderOrderId]);

            var creationDateTime = DateTime.UtcNow;
            var requestTime = DateTime.UtcNow.AddMinutes(5);

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(mainOrderId)
                .SetOrderChainId(orderChainId)
                .SetCreator(new Creator("ttd"))
                .SetType(OrderType.Notification)
                .SetRequestedSendTime(requestTime)
                .SetSendersReference("ref-P5Q7R9S1")
                .SetIdempotencyId("A1B2C3D4-E5F6-G7H8-I9J0-K1L2M3N4O5P6")
                .SetConditionEndpoint(new Uri("https://vg.no/condition"))
                .SetRecipient(new NotificationRecipient
                {
                    RecipientPerson = new RecipientPerson
                    {
                        IgnoreReservation = true,
                        NationalIdentityNumber = "18874198354",
                        ResourceId = "urn:altinn:resource:D208D0E6E5B4",
                        ChannelSchema = NotificationChannel.EmailPreferred,

                        EmailSettings = new EmailSendingOptions
                        {
                            Body = "Main email body",
                            Subject = "Main email subject",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime,
                            SenderEmailAddress = "Main email sender address"
                        },
                        SmsSettings = new SmsSendingOptions
                        {
                            Body = "Main SMS body",
                            Sender = "Main SMS sender",
                            SendingTimePolicy = SendingTimePolicy.Daytime
                        }
                    }
                })
                .SetReminders(
                [
                    new NotificationReminder
                    {
                        DelayDays = 3,
                        Type = OrderType.Reminder,
                        OrderId = firstReminderOrderId,
                        RequestedSendTime = requestTime.AddDays(3),
                        SendersReference = "ref-reminder-B4C6D8E0",
                        ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),
                        Recipient = new NotificationRecipient
                        {
                            RecipientPerson = new RecipientPerson
                            {
                                IgnoreReservation = true,
                                NationalIdentityNumber = "18874198354",
                                ResourceId = "urn:altinn:resource:D208D0E6E5B4",
                                ChannelSchema = NotificationChannel.EmailPreferred,

                                EmailSettings = new EmailSendingOptions
                                {
                                    Body = "First reminder email body",
                                    Subject = "First reminder email subject",
                                    SenderEmailAddress = "sender@example.com",
                                    ContentType = EmailContentType.Html,
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                },
                                SmsSettings = new SmsSendingOptions
                                {
                                    Body = "First reminder SMS body",
                                    Sender = "First reminder SMS sender",
                                    SendingTimePolicy = SendingTimePolicy.Daytime
                                }
                            }
                        }
                    },
                    new NotificationReminder
                    {
                        DelayDays = 7,
                        Type = OrderType.Reminder,
                        OrderId = secondReminderOrderId,
                        RequestedSendTime = requestTime.AddDays(7),
                        SendersReference = "ref-reminder-F8G0H2I4",
                        ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),
                        Recipient = new NotificationRecipient
                        {
                            RecipientPerson = new RecipientPerson
                            {
                                IgnoreReservation = true,
                                NationalIdentityNumber = "18874198354",
                                ResourceId = "urn:altinn:resource:D208D0E6E5B4",
                                ChannelSchema = NotificationChannel.SmsPreferred,

                                SmsSettings = new SmsSendingOptions
                                {
                                    Body = "Second reminder SMS body",
                                    Sender = "Second reminder SMS sender",
                                    SendingTimePolicy = SendingTimePolicy.Daytime
                                },
                                EmailSettings = new EmailSendingOptions
                                {
                                    Body = "Second reminder email body",
                                    Subject = "Second reminder email subject",
                                    SenderEmailAddress = "Second reminder email sender address",
                                    ContentType = EmailContentType.Plain,
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                }
                            }
                        }
                    }
                ])
                .Build();

            NotificationOrder mainOrder = new()
            {
                Id = mainOrderId,
                Creator = new("ttd"),
                IgnoreReservation = true,
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestTime,
                SendersReference = "ref-P5Q7R9S1",
                ResourceId = "urn:altinn:resource:D208D0E6E5B4",
                ConditionEndpoint = new Uri("https://vg.no/condition"),
                NotificationChannel = NotificationChannel.EmailPreferred,
                Templates =
                [
                    new SmsTemplate("Main SMS sender", "Main SMS body"),
                    new EmailTemplate("Main email sender address", "Main email subject", "Main email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([], null, "18874198354")
                ]
            };

            List<NotificationOrder> reminders =
            [
                new NotificationOrder
                {
                    Creator = new("ttd"),
                    IgnoreReservation = true,
                    Id = firstReminderOrderId,
                    Created = creationDateTime,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestTime.AddDays(3),
                    SendersReference = "ref-reminder-B4C6D8E0",
                    ResourceId = "urn:altinn:resource:D208D0E6E5B4",
                    NotificationChannel = NotificationChannel.EmailPreferred,
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),
                    Templates =
                    [
                        new SmsTemplate("First reminder SMS sender", "First reminder SMS body"),
                        new EmailTemplate("sender@example.com", "First reminder email subject", "First reminder email body", EmailContentType.Plain)
                    ],
                    Recipients =
                    [
                        new Recipient([], null, "18874198354")
                    ]
                },
                new NotificationOrder
                {
                    Creator = new("ttd"),
                    IgnoreReservation = false,
                    Created = creationDateTime,
                    Id = secondReminderOrderId,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestTime.AddDays(7),
                    SendersReference = "ref-reminder-F8G0H2I4",
                    ResourceId = "urn:altinn:resource:D208D0E6E5B4",
                    NotificationChannel = NotificationChannel.SmsPreferred,
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),
                    Templates =
                    [
                        new SmsTemplate("Second reminder SMS sender", "Second reminder SMS body"),
                        new EmailTemplate("Second reminder email sender address", "Second reminder email subject", "Second reminder email body", EmailContentType.Plain)
                    ],
                    Recipients =
                    [
                        new Recipient([], null, "18874198354")
                    ]
                }
            ];

            // Act
            var result = await repo.Create(orderRequest, mainOrder, reminders);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(mainOrderId, result[0].Id);
            Assert.NotEqual(mainOrderId, result[1].Id);
            Assert.NotEqual(mainOrderId, result[2].Id);
            Assert.NotEqual(orderChainId, result[0].Id);
            Assert.NotEqual(orderChainId, result[1].Id);
            Assert.NotEqual(orderChainId, result[2].Id);
            Assert.Equal(firstReminderOrderId, result[1].Id);
            Assert.Equal(secondReminderOrderId, result[2].Id);

            string mainOrdersChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            string mainOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{mainOrderId}' and type = 'Notification'";
            string firstReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstReminderOrderId}' and type = 'Reminder'";
            string secondReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondReminderOrderId}' and type = 'Reminder'";

            int mainOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrderSql);
            int firstReminderCount = await PostgreUtil.RunSqlReturnOutput<int>(firstReminderSql);
            int secondReminderCount = await PostgreUtil.RunSqlReturnOutput<int>(secondReminderSql);
            int mainOrdersChainCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrdersChainSql);

            Assert.Equal(1, mainOrderCount);
            Assert.Equal(1, firstReminderCount);
            Assert.Equal(1, secondReminderCount);
            Assert.Equal(1, mainOrdersChainCount);

            // Verify email and SMS templates were persisted correctly
            string mainSmsSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{mainOrderId}'";
            string mainEmailSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{mainOrderId}'";

            string firstReminderSmsSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{firstReminderOrderId}'";
            string firstReminderEmailSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{firstReminderOrderId}'";

            string secondReminderSmsSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{secondReminderOrderId}'";
            string secondReminderEmailSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{secondReminderOrderId}'";

            int mainSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(mainSmsSql);
            int mainEmailCount = await PostgreUtil.RunSqlReturnOutput<int>(mainEmailSql);

            int firstReminderSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(firstReminderSmsSql);
            int firstReminderEmailCount = await PostgreUtil.RunSqlReturnOutput<int>(firstReminderEmailSql);

            int secondReminderSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(secondReminderSmsSql);
            int secondReminderEmailCount = await PostgreUtil.RunSqlReturnOutput<int>(secondReminderEmailSql);

            Assert.Equal(1, mainSmsCount);
            Assert.Equal(1, mainEmailCount);

            Assert.Equal(1, firstReminderSmsCount);
            Assert.Equal(1, firstReminderEmailCount);

            Assert.Equal(1, secondReminderSmsCount);
            Assert.Equal(1, secondReminderEmailCount);
        }

        [Fact]
        public async Task Create_NotificationOrderChain_WithOrganizationRecipient_WithReminders_VerifiesDatabasePersistence()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid mainOrderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();
            Guid firstReminderOrderId = Guid.NewGuid();
            Guid secondReminderOrderId = Guid.NewGuid();

            _ordersChainIdsToDelete.AddRange(orderChainId);
            _orderIdsToDelete.AddRange([mainOrderId, firstReminderOrderId, secondReminderOrderId]);

            var creationDateTime = DateTime.UtcNow;
            var requestTime = DateTime.UtcNow.AddMinutes(5);

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(mainOrderId)
                .SetOrderChainId(orderChainId)
                .SetCreator(new Creator("ttd"))
                .SetType(OrderType.Notification)
                .SetRequestedSendTime(requestTime)
                .SetSendersReference("ref-ORG-A2B4C6D8")
                .SetIdempotencyId("E1F2G3H4-I5J6-K7L8-M9N0-O1P2Q3R4S5T6")
                .SetConditionEndpoint(new Uri("https://vg.no/condition"))
                .SetRecipient(new NotificationRecipient
                {
                    RecipientOrganization = new RecipientOrganization
                    {
                        OrgNumber = "910568183",
                        ResourceId = "urn:altinn:resource:T482D7F1A93C",
                        ChannelSchema = NotificationChannel.EmailPreferred,

                        EmailSettings = new EmailSendingOptions
                        {
                            Body = "Main email body",
                            Subject = "Main email subject",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime,
                            SenderEmailAddress = "Main email sender address"
                        },
                        SmsSettings = new SmsSendingOptions
                        {
                            Body = "Main SMS body",
                            Sender = "Main SMS sender",
                            SendingTimePolicy = SendingTimePolicy.Daytime
                        }
                    }
                })
                .SetReminders(
                [
                    new NotificationReminder
                    {
                        DelayDays = 3,
                        Type = OrderType.Reminder,
                        OrderId = firstReminderOrderId,
                        RequestedSendTime = requestTime.AddDays(3),
                        SendersReference = "ref-reminder-ORG-X2Y4Z6",
                        ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),
                        Recipient = new NotificationRecipient
                        {
                            RecipientOrganization = new RecipientOrganization
                            {
                                OrgNumber = "910568183",
                                ResourceId = "urn:altinn:resource:T482D7F1A93C",
                                ChannelSchema = NotificationChannel.EmailPreferred,

                                EmailSettings = new EmailSendingOptions
                                {
                                    Body = "First reminder email body",
                                    Subject = "First reminder email subject",
                                    SenderEmailAddress = "sender@example.com",
                                    ContentType = EmailContentType.Html,
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                },
                                SmsSettings = new SmsSendingOptions
                                {
                                    Body = "First reminder SMS body",
                                    Sender = "First reminder SMS sender",
                                    SendingTimePolicy = SendingTimePolicy.Daytime
                                }
                            }
                        }
                    },
                    new NotificationReminder
                    {
                        DelayDays = 7,
                        Type = OrderType.Reminder,
                        OrderId = secondReminderOrderId,
                        RequestedSendTime = requestTime.AddDays(7),
                        SendersReference = "ref-reminder-ORG-U2V4W6",
                        ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),
                        Recipient = new NotificationRecipient
                        {
                            RecipientOrganization = new RecipientOrganization
                            {
                                OrgNumber = "910568183",
                                ResourceId = "urn:altinn:resource:T482D7F1A93C",
                                ChannelSchema = NotificationChannel.SmsPreferred,

                                SmsSettings = new SmsSendingOptions
                                {
                                    Body = "Second reminder SMS body",
                                    Sender = "Second reminder SMS sender",
                                    SendingTimePolicy = SendingTimePolicy.Daytime
                                },
                                EmailSettings = new EmailSendingOptions
                                {
                                    Body = "Second reminder email body",
                                    Subject = "Second reminder email subject",
                                    SenderEmailAddress = "Second reminder email sender address",
                                    ContentType = EmailContentType.Plain,
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                }
                            }
                        }
                    }
                ])
                .Build();

            NotificationOrder mainOrder = new()
            {
                Id = mainOrderId,
                Creator = new("ttd"),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestTime,
                SendersReference = "ref-ORG-A2B4C6D8",
                ResourceId = "urn:altinn:resource:T482D7F1A93C",
                ConditionEndpoint = new Uri("https://vg.no/condition"),
                NotificationChannel = NotificationChannel.EmailPreferred,
                Templates =
                [
                    new SmsTemplate("Main SMS sender", "Main SMS body"),
                    new EmailTemplate("Main email sender address", "Main email subject", "Main email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([], organizationNumber: "910568183")
                ]
            };

            List<NotificationOrder> reminders =
            [
                new NotificationOrder
                {
                    Creator = new("ttd"),
                    Id = firstReminderOrderId,
                    Created = creationDateTime,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestTime.AddDays(3),
                    SendersReference = "ref-reminder-ORG-X2Y4Z6",
                    ResourceId = "urn:altinn:resource:T482D7F1A93C",
                    NotificationChannel = NotificationChannel.EmailPreferred,
                    ConditionEndpoint = new Uri("https://vg.no/first-reminder-condition"),
                    Templates =
                    [
                        new SmsTemplate("First reminder SMS sender", "First reminder SMS body"),
                        new EmailTemplate("sender@example.com", "First reminder email subject", "First reminder email body", EmailContentType.Plain)
                    ],
                    Recipients =
                    [
                        new Recipient([], organizationNumber: "910568183")
                    ]
                },
                new NotificationOrder
                {
                    Creator = new("ttd"),
                    Created = creationDateTime,
                    Id = secondReminderOrderId,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestTime.AddDays(7),
                    SendersReference = "ref-reminder-ORG-U2V4W6",
                    ResourceId = "urn:altinn:resource:T482D7F1A93C",
                    NotificationChannel = NotificationChannel.SmsPreferred,
                    ConditionEndpoint = new Uri("https://vg.no/second-reminder-condition"),
                    Templates =
                    [
                        new SmsTemplate("Second reminder SMS sender", "Second reminder SMS body"),
                        new EmailTemplate("Second reminder email sender address", "Second reminder email subject", "Second reminder email body", EmailContentType.Plain)
                    ],
                    Recipients =
                    [
                        new Recipient([], organizationNumber: "910568183")
                    ]
                }
            ];

            // Act
            var result = await repo.Create(orderRequest, mainOrder, reminders);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(mainOrderId, result[0].Id);
            Assert.NotEqual(mainOrderId, result[1].Id);
            Assert.NotEqual(mainOrderId, result[2].Id);
            Assert.NotEqual(orderChainId, result[0].Id);
            Assert.NotEqual(orderChainId, result[1].Id);
            Assert.NotEqual(orderChainId, result[2].Id);
            Assert.Equal(firstReminderOrderId, result[1].Id);
            Assert.Equal(secondReminderOrderId, result[2].Id);

            string mainOrdersChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            string mainOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{mainOrderId}' and type = 'Notification'";
            string firstReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstReminderOrderId}' and type = 'Reminder'";
            string secondReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondReminderOrderId}' and type = 'Reminder'";

            int mainOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrderSql);
            int firstReminderCount = await PostgreUtil.RunSqlReturnOutput<int>(firstReminderSql);
            int secondReminderCount = await PostgreUtil.RunSqlReturnOutput<int>(secondReminderSql);
            int mainOrdersChainCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrdersChainSql);

            Assert.Equal(1, mainOrderCount);
            Assert.Equal(1, firstReminderCount);
            Assert.Equal(1, secondReminderCount);
            Assert.Equal(1, mainOrdersChainCount);

            // Verify email and SMS templates were persisted correctly
            string mainSmsSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{mainOrderId}'";
            string mainEmailSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{mainOrderId}'";

            string firstReminderSmsSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{firstReminderOrderId}'";
            string firstReminderEmailSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{firstReminderOrderId}'";

            string secondReminderSmsSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{secondReminderOrderId}'";
            string secondReminderEmailSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{secondReminderOrderId}'";

            int mainSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(mainSmsSql);
            int mainEmailCount = await PostgreUtil.RunSqlReturnOutput<int>(mainEmailSql);

            int firstReminderSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(firstReminderSmsSql);
            int firstReminderEmailCount = await PostgreUtil.RunSqlReturnOutput<int>(firstReminderEmailSql);

            int secondReminderSmsCount = await PostgreUtil.RunSqlReturnOutput<int>(secondReminderSmsSql);
            int secondReminderEmailCount = await PostgreUtil.RunSqlReturnOutput<int>(secondReminderEmailSql);

            Assert.Equal(1, mainSmsCount);
            Assert.Equal(1, mainEmailCount);

            Assert.Equal(1, firstReminderSmsCount);
            Assert.Equal(1, firstReminderEmailCount);

            Assert.Equal(1, secondReminderSmsCount);
            Assert.Equal(1, secondReminderEmailCount);
        }

        [Fact]
        public async Task Create_NotificationOrderChain_WithEmailRecipient_NoReminders_WithInvalidTemplates_ThrowsNullReferenceException()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            // Create a valid chain request
            var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetOrderChainId(orderChainId)
                .SetCreator(new Creator("skd"))
                .SetType(OrderType.Notification)
                .SetIdempotencyId("EXCEPTION-TEST-ID")
                .SetRequestedSendTime(requestedSendTime)
                .SetSendersReference("EXCEPTION-TEST-REF")
                .SetRecipient(new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = "recipient@example.com",
                        Settings = new EmailSendingOptions
                        {
                            Body = "Email body",
                            Subject = "Email subject",
                            SenderEmailAddress = "Email sender address",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime
                        }
                    }
                })
                .Build();

            // An invalid order with null Templates to cause an exception.
            NotificationOrder invalidOrder = new()
            {
                Id = orderId,
                Creator = new("skd"),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Email,
                SendersReference = "EXCEPTION-TEST-REF",
                Templates = null!, // This will cause an exception
                Recipients =
                [
                    new Recipient([new EmailAddressPoint("recipient@example.com")])
                ]
            };

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(async () => await repo.Create(orderChainRequest, invalidOrder, null));
        }

        [Fact]
        public async Task Create_NotificationOrderChain_WithEmailRecipient_NoReminders_WhenCancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetOrderChainId(orderChainId)
                .SetCreator(new Creator("skd"))
                .SetType(OrderType.Notification)
                .SetRequestedSendTime(requestedSendTime)
                .SetIdempotencyId("CANCELLATION-TEST-ID")
                .SetSendersReference("CANCELLATION-TEST-REF")
                .SetRecipient(new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = "recipient@example.com",
                        Settings = new EmailSendingOptions
                        {
                            Body = "Email body",
                            Subject = "Email subject",
                            SenderEmailAddress = "Email sender address",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime
                        }
                    }
                })
                .Build();

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new("skd"),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestedSendTime,
                SendersReference = "CANCELLATION-TEST-REF",
                NotificationChannel = NotificationChannel.Email,
                Templates =
                [
                    new EmailTemplate("Email sender address", "Email subject", "Email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([new EmailAddressPoint("recipient@example.com")])
                ]
            };

            // Create a cancellation token that's already cancelled
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await repo.Create(orderChainRequest, notificationOrder, null, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task GetOrderChainTracking_WhenNonExistentCreatorAndIdempotencyId_ReturnsNull()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            string creatorName = "non-existent-creator";
            string idempotencyId = "non-existent-id";

            // Act
            var result = await repo.GetOrderChainTracking(creatorName, idempotencyId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetOrderChainTracking_WhenCancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            string creatorName = "test-creator";
            string idempotencyId = "test-idempotency-id";

            // Create a cancellation token that's already cancelled
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await repo.GetOrderChainTracking(creatorName, idempotencyId, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task GetOrderChainTracking_MainOrderWithoutSendersReference_HandlesNullSendersReference()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            string creator = "creator-A3A0F691111";
            string idempotencyId = "idempotency-BB8C6E067068";

            DateTime creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            // Create the chain request and order without sender's reference
            var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetOrderChainId(orderChainId)
                .SetIdempotencyId(idempotencyId)
                .SetType(OrderType.Notification)
                .SetCreator(new Creator(creator))
                .SetRequestedSendTime(requestedSendTime)
                .SetRecipient(new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = "recipient@example.com",
                        Settings = new EmailSendingOptions
                        {
                            Body = "Email body",
                            Subject = "Email subject",
                            SenderEmailAddress = "Email sender address",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime
                        }
                    }
                })
                .Build();

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new(creator),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Email,
                Templates =
                [
                    new EmailTemplate("Email sender address", "Email subject", "Email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([new EmailAddressPoint("recipient@example.com")])
                ]
            };

            await repo.Create(orderChainRequest, notificationOrder, null);

            // Act
            var result = await repo.GetOrderChainTracking(creator, idempotencyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(orderChainId, result.OrderChainId);
            Assert.Null(result.OrderChainReceipt.Reminders);
            Assert.Null(result.OrderChainReceipt.SendersReference);
            Assert.Equal(orderId, result.OrderChainReceipt.ShipmentId);
        }

        [Fact]
        public async Task GetOrderChainTracking_WhenNotificationOrderWithReminderMissingReference_ReturnsNullReferenceForReminder()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid reminderId = Guid.NewGuid();
            Guid mainOrderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            string creator = "tracking-211BBFC9BDBB";
            string idempotencyId = "TRACKING-E18557E17D42";

            DateTime creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            _ordersChainIdsToDelete.Add(orderChainId);
            _orderIdsToDelete.AddRange([mainOrderId, reminderId]);

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(mainOrderId)
                .SetOrderChainId(orderChainId)
                .SetIdempotencyId(idempotencyId)
                .SetType(OrderType.Notification)
                .SetCreator(new Creator(creator))
                .SetRequestedSendTime(requestedSendTime)
                .SetSendersReference("MAIN-ORDER-REF-NO-REMINDER-REF")
                .SetRecipient(new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = "recipient@example.com",
                        Settings = new EmailSendingOptions
                        {
                            Body = "Main email body",
                            Subject = "Main email subject",
                            SenderEmailAddress = "sender@example.com",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime
                        }
                    }
                })
                .SetReminders(
                [
                    new NotificationReminder
                    {
                        DelayDays = 3,
                        OrderId = reminderId,
                        Type = OrderType.Reminder,
                        RequestedSendTime = requestedSendTime.AddDays(3),
                        Recipient = new NotificationRecipient
                        {
                            RecipientEmail = new RecipientEmail
                            {
                                EmailAddress = "recipient@example.com",
                                Settings = new EmailSendingOptions
                                {
                                    Body = "Reminder without reference body",
                                    Subject = "Reminder without reference subject",
                                    SenderEmailAddress = "sender@example.com",
                                    ContentType = EmailContentType.Plain,
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                }
                            }
                        }
                    }
                ])
                .Build();

            // Create the main notification order
            NotificationOrder mainOrder = new()
            {
                Id = mainOrderId,
                Creator = new(creator),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Email,
                SendersReference = "MAIN-ORDER-REF-NO-REMINDER-REF",
                Templates =
                [
                    new EmailTemplate("sender@example.com", "Main email subject", "Main email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([new EmailAddressPoint("recipient@example.com")])
                ]
            };

            // Create reminder order without SendersReference
            List<NotificationOrder> reminders =
            [
                new NotificationOrder
                {
                    Id = reminderId,
                    Creator = new(creator),
                    Created = creationDateTime,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestedSendTime.AddDays(3),
                    NotificationChannel = NotificationChannel.Email,
                    Templates =
                    [
                        new EmailTemplate("sender@example.com", "Reminder without reference subject", "Reminder without reference body", EmailContentType.Plain)
                    ],
                    Recipients =
                    [
                        new Recipient([new EmailAddressPoint("recipient@example.com")])
                    ]
                }
            ];

            // Inserts the order chain with reminder in the database.
            await repo.Create(orderRequest, mainOrder, reminders);

            // Act
            var result = await repo.GetOrderChainTracking(creator, idempotencyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(orderChainId, result.OrderChainId);
            Assert.Equal(mainOrderId, result.OrderChainReceipt.ShipmentId);
            Assert.Equal("MAIN-ORDER-REF-NO-REMINDER-REF", result.OrderChainReceipt.SendersReference);

            // Verify reminder
            Assert.NotNull(result.OrderChainReceipt.Reminders);
            Assert.Single(result.OrderChainReceipt.Reminders);
            Assert.Null(result.OrderChainReceipt.Reminders[0].SendersReference);
            Assert.Equal(reminderId, result.OrderChainReceipt.Reminders[0].ShipmentId);
        }

        [Fact]
        public async Task GetOrderChainTracking_WhenNotificationOrderChainWithRemindersExists_ReturnsCorrectOrderChainTrackingInformation()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid mainOrderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();
            Guid firstReminderId = Guid.NewGuid();
            Guid secondReminderId = Guid.NewGuid();

            string creator = "tracking-test-reminders";
            string idempotencyId = "TRACKING-30E3CD3997E9";

            DateTime creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            _ordersChainIdsToDelete.Add(orderChainId);
            _orderIdsToDelete.AddRange([mainOrderId, firstReminderId, secondReminderId]);

            var orderRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(mainOrderId)
                .SetOrderChainId(orderChainId)
                .SetIdempotencyId(idempotencyId)
                .SetCreator(new Creator(creator))
                .SetType(OrderType.Notification)
                .SetSendersReference("MAIN-ORDER-REF")
                .SetRequestedSendTime(requestedSendTime)
                .SetRecipient(new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = "recipient@example.com",
                        Settings = new EmailSendingOptions
                        {
                            Body = "Main email body",
                            Subject = "Main email subject",
                            SenderEmailAddress = "sender@example.com",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime
                        }
                    }
                })
                .SetReminders(
                [
                    new NotificationReminder
                    {
                        DelayDays = 3,
                        OrderId = firstReminderId,
                        Type = OrderType.Reminder,
                        SendersReference = "FIRST-REMINDER-REF",
                        RequestedSendTime = requestedSendTime.AddDays(3),
                        Recipient = new NotificationRecipient
                        {
                            RecipientEmail = new RecipientEmail
                            {
                                EmailAddress = "recipient@example.com",
                                Settings = new EmailSendingOptions
                                {
                                    Body = "First reminder email body",
                                    Subject = "First reminder email subject",
                                    SenderEmailAddress = "sender@example.com",
                                    ContentType = EmailContentType.Plain,
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                }
                            }
                        }
                    },
                    new NotificationReminder
                    {
                        DelayDays = 7,
                        OrderId = secondReminderId,
                        Type = OrderType.Reminder,
                        SendersReference = "SECOND-REMINDER-REF",
                        RequestedSendTime = requestedSendTime.AddDays(7),
                        Recipient = new NotificationRecipient
                        {
                            RecipientEmail = new RecipientEmail
                            {
                                EmailAddress = "recipient@example.com",
                                Settings = new EmailSendingOptions
                                {
                                    Body = "Second reminder email body",
                                    Subject = "Second reminder email subject",
                                    SenderEmailAddress = "sender@example.com",
                                    ContentType = EmailContentType.Plain,
                                    SendingTimePolicy = SendingTimePolicy.Anytime
                                }
                            }
                        }
                    }
                ])
                .Build();

            // Create the main notification order
            NotificationOrder mainOrder = new()
            {
                Id = mainOrderId,
                Creator = new(creator),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Email,
                SendersReference = "MAIN-ORDER-REF",
                Templates =
                [
                    new EmailTemplate("sender@example.com", "Main email subject", "Main email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([new EmailAddressPoint("recipient@example.com")])
                ]
            };

            // Create reminder orders
            List<NotificationOrder> reminders =
            [
                new NotificationOrder
                {
                    Id = firstReminderId,
                    Creator = new(creator),
                    Created = creationDateTime,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestedSendTime.AddDays(3),
                    NotificationChannel = NotificationChannel.Email,
                    SendersReference = "FIRST-REMINDER-REF",
                    Templates =
                    [
                        new EmailTemplate("sender@example.com", "First reminder email subject", "First reminder email body", EmailContentType.Plain)
                    ],
                    Recipients =
                    [
                        new Recipient([new EmailAddressPoint("recipient@example.com")])
                    ]
                },
                new NotificationOrder
                {
                    Id = secondReminderId,
                    Creator = new(creator),
                    Created = creationDateTime,
                    Type = OrderType.Reminder,
                    RequestedSendTime = requestedSendTime.AddDays(7),
                    NotificationChannel = NotificationChannel.Email,
                    SendersReference = "SECOND-REMINDER-REF",
                    Templates =
                    [
                        new EmailTemplate("sender@example.com", "Second reminder email subject", "Second reminder email body", EmailContentType.Plain)
                    ],
                    Recipients =
                    [
                        new Recipient([new EmailAddressPoint("recipient@example.com")])
                    ]
                }
            ];

            // Insert the order chain with reminders in the database
            await repo.Create(orderRequest, mainOrder, reminders);

            // Act
            var result = await repo.GetOrderChainTracking(creator, idempotencyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(orderChainId, result.OrderChainId);
            Assert.Equal(mainOrderId, result.OrderChainReceipt.ShipmentId);
            Assert.Equal("MAIN-ORDER-REF", result.OrderChainReceipt.SendersReference);

            // Verify reminders
            Assert.NotNull(result.OrderChainReceipt.Reminders);
            Assert.Equal(2, result.OrderChainReceipt.Reminders.Count);

            // First reminder
            Assert.Equal(firstReminderId, result.OrderChainReceipt.Reminders[0].ShipmentId);
            Assert.Equal("FIRST-REMINDER-REF", result.OrderChainReceipt.Reminders[0].SendersReference);

            // Second reminder
            Assert.Equal(secondReminderId, result.OrderChainReceipt.Reminders[1].ShipmentId);
            Assert.Equal("SECOND-REMINDER-REF", result.OrderChainReceipt.Reminders[1].SendersReference);
        }

        [Fact]
        public async Task GetOrderChainTracking_WhenNotificationOrderChainWithoutRemindersExists_ReturnsCorrectOrderChainTrackingInformation()
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            string creator = "random-creator-name";
            string idempotencyId = "random-39B0068652C6";

            DateTime creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(10);

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
                .SetOrderId(orderId)
                .SetOrderChainId(orderChainId)
                .SetIdempotencyId(idempotencyId)
                .SetCreator(new Creator(creator))
                .SetType(OrderType.Notification)
                .SetRequestedSendTime(requestedSendTime)
                .SetSendersReference("TRACKING-C69C615A8412")
                .SetRecipient(new NotificationRecipient
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = "recipient@example.com",
                        Settings = new EmailSendingOptions
                        {
                            Body = "Email body",
                            Subject = "Email subject",
                            SenderEmailAddress = "Email sender address",
                            ContentType = EmailContentType.Plain,
                            SendingTimePolicy = SendingTimePolicy.Anytime
                        }
                    }
                })
                .Build();

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new(creator),
                Created = creationDateTime,
                Type = OrderType.Notification,
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Email,
                SendersReference = "TRACKING-C69C615A8412",
                Templates =
                [
                    new EmailTemplate("Email sender address", "Email subject", "Email body", EmailContentType.Plain)
                ],
                Recipients =
                [
                    new Recipient([new EmailAddressPoint("recipient@example.com")])
                ]
            };

            // Insert the order chain in the database
            await repo.Create(orderChainRequest, notificationOrder, null);

            // Act
            var result = await repo.GetOrderChainTracking(creator, idempotencyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(orderChainId, result.OrderChainId);
            Assert.Null(result.OrderChainReceipt.Reminders);
            Assert.Equal(orderId, result.OrderChainReceipt.ShipmentId);
            Assert.Equal("TRACKING-C69C615A8412", result.OrderChainReceipt.SendersReference);
        }

        [Theory]
        [InlineData(AlternateIdentifierSource.Sms)]
        [InlineData(AlternateIdentifierSource.Email)]
        [InlineData(AlternateIdentifierSource.Order)]
        public async Task TryCompleteOrderBasedOnNotificationsState_WithNullNotificationId_ReturnsFalse(AlternateIdentifierSource alternateIdentifierSource)
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            // Act
            bool result = await repo.TryCompleteOrderBasedOnNotificationsState(null, alternateIdentifierSource);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(OrderProcessingStatus.Processed, OrderProcessingStatus.Completed, true)]
        [InlineData(OrderProcessingStatus.Cancelled, OrderProcessingStatus.Cancelled, false)]
        [InlineData(OrderProcessingStatus.Completed, OrderProcessingStatus.Completed, false)]
        [InlineData(OrderProcessingStatus.Processing, OrderProcessingStatus.Completed, true)]
        [InlineData(OrderProcessingStatus.Registered, OrderProcessingStatus.Registered, false)]
        [InlineData(OrderProcessingStatus.SendConditionNotMet, OrderProcessingStatus.SendConditionNotMet, false)]
        public async Task TryCompleteOrderBasedOnNotificationsState_VerifiesStateTransitionRules(OrderProcessingStatus currentStatus, OrderProcessingStatus expectedStatus, bool shouldUpdateStatus)
        {
            // Arrange
            OrderRepository repo = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid notificationOrderId = Guid.NewGuid();

            _orderIdsToDelete.Add(notificationOrderId);

            var creationDateTime = DateTime.UtcNow;
            var requestedSendTime = DateTime.UtcNow.AddMinutes(5);

            NotificationOrder notificationOrder = new()
            {
                Creator = new("ttd"),
                IgnoreReservation = true,
                Id = notificationOrderId,
                Created = creationDateTime,
                Type = OrderType.Notification,
                SendersReference = "ref-P5Q7R9S1",
                RequestedSendTime = requestedSendTime,
                SendingTimePolicy = SendingTimePolicy.Anytime,
                ResourceId = "urn:altinn:resource:D208D0E6E5B4",
                ConditionEndpoint = new Uri("https://vg.no/condition"),
                NotificationChannel = NotificationChannel.EmailPreferred,

                Templates =
                [
                    new SmsTemplate("Main SMS sender", "Main SMS body"),
                    new EmailTemplate("Main email sender address", "Main email subject", "Main email body", EmailContentType.Plain)
                ],

                Recipients =
                [
                    new Recipient([], null, "18874198354")
                ]
            };

            var orderCreationResult = await repo.Create(notificationOrder);
            switch (currentStatus)
            {
                case OrderProcessingStatus.Registered:
                    break;

                case OrderProcessingStatus.Completed:
                case OrderProcessingStatus.Cancelled:
                case OrderProcessingStatus.Processed:
                case OrderProcessingStatus.Processing:
                case OrderProcessingStatus.SendConditionNotMet:
                    await repo.SetProcessingStatus(notificationOrderId, currentStatus);
                    break;
            }

            // Act
            bool statusUpdatingResult = await repo.TryCompleteOrderBasedOnNotificationsState(notificationOrderId, AlternateIdentifierSource.Order);
            string mainOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{notificationOrderId}' and type = 'Notification' and processedstatus = '{expectedStatus}'";
            int mainOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(mainOrderSql);

            // Assert
            Assert.Equal(1, mainOrderCount);
            Assert.NotNull(orderCreationResult);
            Assert.Equal(shouldUpdateStatus, statusUpdatingResult);
        }

        [Fact]
        public async Task Create_WithValidInstantNotificationOrder_OrdersSuccessfullyPersisted()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var creationDateTime = DateTime.UtcNow;

            var instantNotificationOrder = new InstantNotificationOrder
            {
                OrderId = orderId,
                Creator = new("ttd"),
                Created = creationDateTime,
                OrderChainId = orderChainId,
                IdempotencyId = "F6E76FA5-0A53-4195-A702-21ECCC77B9E8",
                SendersReference = "DAFA7290-27AE-4958-8CAA-A1F97B6B2307",

                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        PhoneNumber = "+4799999999",
                        TimeToLiveInSeconds = 3600,
                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "This is an urgent test message"
                        }
                    }
                }
            };

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new("ttd"),
                Type = OrderType.Instant,
                Created = creationDateTime,
                RequestedSendTime = creationDateTime,
                NotificationChannel = NotificationChannel.Sms,
                SendersReference = "DAFA7290-27AE-4958-8CAA-A1F97B6B2307",
                Templates =
                [
                    new SmsTemplate("Altinn", "This is an urgent test message")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            var smsNotification = new SmsNotification
            {
                OrderId = orderId,
                Id = Guid.NewGuid(),
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Act
            var result = await orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, creationDateTime.AddSeconds(3600), 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(orderChainId, result.OrderChainId);

            Assert.NotNull(result.Notification);
            Assert.Equal(orderId, result.Notification.ShipmentId);
            Assert.Equal("DAFA7290-27AE-4958-8CAA-A1F97B6B2307", result.Notification.SendersReference);

            // Verify database persistence
            string orderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
            Assert.Equal(1, orderChainCount);

            string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}' and type = 'Instant'";
            int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
            Assert.Equal(1, orderCount);

            string smsTextSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int smsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(smsTextSql);
            Assert.Equal(1, smsTextCount);

            string smsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications as sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int smsNotificationsCount = await PostgreUtil.RunSqlReturnOutput<int>(smsNotificationSql);
            Assert.Equal(1, smsNotificationsCount);
        }

        [Fact]
        public async Task Create_InstantNotificationOrderWithDuplicatedIdempotencyId_ThrowsException()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid firstOrderId = Guid.NewGuid();
            Guid secondOrderId = Guid.NewGuid();
            Guid firstOrderChainId = Guid.NewGuid();
            Guid secondOrderChainId = Guid.NewGuid();

            string idempotencyId = "DUPLICATE-IDEMPOTENCY-30BC69F09C38";

            _orderIdsToDelete.AddRange([firstOrderId, secondOrderId]);
            _ordersChainIdsToDelete.AddRange([firstOrderChainId, secondOrderChainId]);

            var creationDateTime = DateTime.UtcNow;

            // First instant notification order
            var firstInstantOrder = new InstantNotificationOrder
            {
                Creator = new("ttd"),
                OrderId = firstOrderId,
                Created = creationDateTime,
                IdempotencyId = idempotencyId,
                OrderChainId = firstOrderChainId,
                SendersReference = "F4B120EF-7DBD-438A-8402-02D21833602B",

                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        PhoneNumber = "+4799999999",
                        TimeToLiveInSeconds = 7200,
                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "First message with duplicate idempotency ID"
                        }
                    }
                }
            };

            NotificationOrder firstNotificationOrder = new()
            {
                Id = firstOrderId,
                Creator = new("ttd"),
                Type = OrderType.Instant,
                Created = creationDateTime,
                RequestedSendTime = creationDateTime,
                NotificationChannel = NotificationChannel.Sms,
                SendersReference = "F4B120EF-7DBD-438A-8402-02D21833602B",
                Templates =
                [
                    new SmsTemplate("Altinn", "First message with duplicate idempotency ID")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            var firstSmsNotification = new SmsNotification
            {
                Id = Guid.NewGuid(),
                OrderId = firstOrderId,
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Save the first order
            await orderRepository.Create(firstInstantOrder, firstNotificationOrder, firstSmsNotification, creationDateTime.AddSeconds(7200), 1);

            // Create second order with same idempotency ID
            var secondInstantOrder = new InstantNotificationOrder
            {
                Creator = new("ttd"),
                OrderId = secondOrderId,
                IdempotencyId = idempotencyId,
                OrderChainId = secondOrderChainId,
                Created = creationDateTime.AddMinutes(10),
                SendersReference = "C075F863-3E89-4688-9B31-D8817FECDF6B",
                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        TimeToLiveInSeconds = 10800,
                        PhoneNumber = "+4788888888",
                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "Second message with duplicate idempotency ID"
                        }
                    }
                }
            };

            NotificationOrder secondNotificationOrder = new()
            {
                Id = secondOrderId,
                Creator = new("ttd"),
                Type = OrderType.Instant,
                Created = creationDateTime.AddMinutes(10),
                NotificationChannel = NotificationChannel.Sms,
                RequestedSendTime = creationDateTime.AddMinutes(10),
                SendersReference = "C075F863-3E89-4688-9B31-D8817FECDF6B",
                Templates =
                [
                    new SmsTemplate("Altinn", "Second message with duplicate idempotency ID")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4788888888")])
                ]
            };

            var secondSmsNotification = new SmsNotification
            {
                Id = Guid.NewGuid(),
                OrderId = secondOrderId,
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(async () => await orderRepository.Create(secondInstantOrder, secondNotificationOrder, secondSmsNotification, creationDateTime.AddSeconds(10800), 1));

            // Verify Orders Chain
            string persistedOrderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{firstOrderChainId}'";
            int persistedOrderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedOrderChainSql);
            Assert.Equal(1, persistedOrderChainCount);

            string notPersistedOrderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{secondOrderChainId}'";
            int notPersistedOrderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedOrderChainSql);
            Assert.Equal(0, notPersistedOrderChainCount);

            // Verify Orders
            string persistedOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstOrderId}' and type = 'Instant'";
            int persistedOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedOrderSql);
            Assert.Equal(1, persistedOrderCount);

            string notPersistedOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondOrderId}' and type = 'Instant'";
            int notPersistedOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedOrderSql);
            Assert.Equal(0, notPersistedOrderCount);

            // Verify SMS texts
            string persistedSmsTextSql = $@"SELECT count(*) FROM notifications.smstexts AS st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{firstOrderId}'";
            int persistedSmsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedSmsTextSql);
            Assert.Equal(1, persistedSmsTextCount);

            string notPersistedSmsTextSql = $@"SELECT count(*) FROM notifications.smstexts AS st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{secondOrderId}'";
            int notPersistedSmsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedSmsTextSql);
            Assert.Equal(0, notPersistedSmsTextCount);

            // Verify SMS notifications
            string persistedSmsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications AS sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{firstOrderId}'";
            int persistedSmsNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedSmsNotificationSql);
            Assert.Equal(1, persistedSmsNotificationCount);

            string notPersistedSmsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications AS sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{secondOrderId}'";
            int notPersistedSmsNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedSmsNotificationSql);
            Assert.Equal(0, notPersistedSmsNotificationCount);
        }

        [Fact]
        public async Task Create_InstantNotificationOrderWithoutSmsTemplate_ThrowsInvalidOperationException()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var creationDateTime = DateTime.UtcNow;

            var instantNotificationOrder = new InstantNotificationOrder
            {
                OrderId = orderId,
                Creator = new("ttd"),
                Created = creationDateTime,
                OrderChainId = orderChainId,
                IdempotencyId = "{4E161804-5D31-41F8-B528-62100E9C5712}",
                SendersReference = "3C001D49-FBF9-4784-88E7-19263E8C20A0",
                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        TimeToLiveInSeconds = 3600,
                        PhoneNumber = "+4799999999",

                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "This message should not be persisted"
                        }
                    }
                }
            };

            // NotificationOrder without any SmsTemplate in Templates
            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new("ttd"),
                Type = OrderType.Instant,
                Created = creationDateTime,
                RequestedSendTime = creationDateTime,
                NotificationChannel = NotificationChannel.Sms,
                SendersReference = "3C001D49-FBF9-4784-88E7-19263E8C20A0",
                Templates = [], // No SmsTemplate
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            var smsNotification = new SmsNotification
            {
                OrderId = orderId,
                Id = Guid.NewGuid(),
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, creationDateTime.AddSeconds(3600), 1));

            // Verify nothing was persisted
            string orderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
            Assert.Equal(0, orderChainCount);

            string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}'";
            int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
            Assert.Equal(0, orderCount);

            string smsTextSql = $@"SELECT count(*) FROM notifications.smstexts AS st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int smsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(smsTextSql);
            Assert.Equal(0, smsTextCount);

            string smsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications AS sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int smsNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(smsNotificationSql);
            Assert.Equal(0, smsNotificationCount);
        }

        [Fact]
        public async Task Create_InstantNotificationOrderWithCancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var creationDateTime = DateTime.UtcNow;

            var instantNotificationOrder = new InstantNotificationOrder
            {
                OrderId = orderId,
                Creator = new("ttd"),
                Created = creationDateTime,
                OrderChainId = orderChainId,
                IdempotencyId = "INSTANT-CANCEL-1E3CD83E99FD",
                SendersReference = "INSTANT-CANCEL-4B8FE77B9455",
                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        TimeToLiveInSeconds = 9000,
                        PhoneNumber = "+4799999999",
                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "This message should not be persisted"
                        }
                    }
                }
            };

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new("ttd"),
                Type = OrderType.Instant,
                Created = creationDateTime,
                RequestedSendTime = creationDateTime,
                NotificationChannel = NotificationChannel.Sms,
                SendersReference = "INSTANT-CANCEL-4B8FE77B9455",
                Templates =
                [
                    new SmsTemplate("Altinn", "This message should not be persisted")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            var smsNotification = new SmsNotification
            {
                OrderId = orderId,
                Id = Guid.NewGuid(),
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Create a cancellation token that's already cancelled
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, creationDateTime.AddSeconds(9000), 1, cancellationTokenSource.Token));

            // Verify nothing was persisted
            string orderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
            Assert.Equal(0, orderChainCount);

            string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}'";
            int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
            Assert.Equal(0, orderCount);

            string smsTextSql = $@"SELECT count(*) FROM notifications.smstexts AS st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int smsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(smsTextSql);
            Assert.Equal(0, smsTextCount);

            string smsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications AS sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int smsNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(smsNotificationSql);
            Assert.Equal(0, smsNotificationCount);
        }

        [Fact]
        public async Task RetrieveTrackingInformation_WhenNonExistentCreatorAndIdempotencyId_ReturnsNull()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            string creatorName = "ttd";
            string idempotencyId = "1091990A-D05D-4326-A1D7-60420F4E8B1E";

            // Act
            var result = await orderRepository.RetrieveTrackingInformation(creatorName, idempotencyId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RetrieveTrackingInformation_WhenCancellationRequested_ThrowsTaskCanceledException()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            string creatorName = "ttd";
            string idempotencyId = "2C2024D9-0A82-4BA5-A71F-17D33D0EFEC9";

            // Create a cancellation token that's already cancelled
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await orderRepository.RetrieveTrackingInformation(creatorName, idempotencyId, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task RetrieveTrackingInformation_WithValidCreatorAndIdempotencyId_ReturnsExpectedOrderDetails()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            var creationDateTime = DateTime.UtcNow;

            string creator = "ttd";
            string idempotencyId = "9EFB6947-BBB1-4DF2-9466-CE44CD1A46B0";
            string sendersReference = "BB69F687-AF95-4790-AB27-DED218B4800B";

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var instantNotificationOrder = new InstantNotificationOrder
            {
                OrderId = orderId,
                Creator = new(creator),
                Created = creationDateTime,
                OrderChainId = orderChainId,
                IdempotencyId = idempotencyId,
                SendersReference = sendersReference,
                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        TimeToLiveInSeconds = 3600,
                        PhoneNumber = "+4799999999",
                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "Test message for tracking"
                        }
                    }
                }
            };

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new(creator),
                Type = OrderType.Instant,
                Created = creationDateTime,
                SendersReference = sendersReference,
                RequestedSendTime = creationDateTime,
                NotificationChannel = NotificationChannel.Sms,
                Templates =
                [
                    new SmsTemplate("Altinn", "Test message for tracking")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            var smsNotification = new SmsNotification
            {
                OrderId = orderId,
                Id = Guid.NewGuid(),
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Create the order in the database
            await orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, creationDateTime.AddMinutes(60), 1);

            // Act
            var result = await orderRepository.RetrieveTrackingInformation(creator, idempotencyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(orderChainId, result.OrderChainId);
            Assert.Equal(orderId, result.Notification.ShipmentId);
            Assert.Equal(sendersReference, result.Notification.SendersReference);
        }

        [Fact]
        public async Task RetrieveTrackingInformation_RequiresMatchingCreatorAndIdempotencyId_ReturnsNullWhenCreatorMismatches()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            var creationDateTime = DateTime.UtcNow;

            string creator = "ttd";
            string invalidCreator = "not-ttd";
            string idempotencyId = "08556351-748F-4B05-A42A-1BA91DD5C275";
            string senderReference = "F15C804E-9C66-4968-916F-9E71C4C6FB63";

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var instantNotificationOrder = new InstantNotificationOrder
            {
                OrderId = orderId,
                Creator = new(creator),
                Created = creationDateTime,
                OrderChainId = orderChainId,
                IdempotencyId = idempotencyId,
                SendersReference = senderReference,
                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        TimeToLiveInSeconds = 3600,
                        PhoneNumber = "+4799999999",
                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "Test message for wrong creator"
                        }
                    }
                }
            };

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new(creator),
                Type = OrderType.Instant,
                Created = creationDateTime,
                SendersReference = senderReference,
                RequestedSendTime = creationDateTime,
                NotificationChannel = NotificationChannel.Sms,
                Templates =
                [
                    new SmsTemplate("Altinn", "Test message for wrong creator")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            var smsNotification = new SmsNotification
            {
                OrderId = orderId,
                Id = Guid.NewGuid(),
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Create the order in the database
            await orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, creationDateTime.AddMinutes(60), 1);

            // Act
            var result = await orderRepository.RetrieveTrackingInformation(invalidCreator, idempotencyId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RetrieveTrackingInformation_RequiresMatchingCreatorAndIdempotencyId_ReturnsNullWhenIdempotencyIdMismatches()
        {
            // Arrange
            OrderRepository orderRepository =
                (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)]).First(i => i.GetType() == typeof(OrderRepository));

            Guid orderId = Guid.NewGuid();
            Guid orderChainId = Guid.NewGuid();

            var creationDateTime = DateTime.UtcNow;

            string creator = "ttd";
            string idempotencyId = "7D4DF1D4-4E55-4BDC-ACA1-0331D47AC28F";
            string senderReference = "CB11C461-8887-4887-9A0B-3878F99D13F6";
            string invalidIdempotencyId = "720D256D-0A2A-4C5F-BD9A-A32634271CD2";

            _orderIdsToDelete.Add(orderId);
            _ordersChainIdsToDelete.Add(orderChainId);

            var instantNotificationOrder = new InstantNotificationOrder
            {
                OrderId = orderId,
                Creator = new(creator),
                Created = creationDateTime,
                OrderChainId = orderChainId,
                IdempotencyId = idempotencyId,
                SendersReference = senderReference,
                InstantNotificationRecipient = new InstantNotificationRecipient
                {
                    ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                    {
                        PhoneNumber = "+4799999999",
                        TimeToLiveInSeconds = 3600,
                        ShortMessageContent = new ShortMessageContent
                        {
                            Sender = "Altinn",
                            Message = "Test message for wrong idempotency identifier"
                        }
                    }
                }
            };

            NotificationOrder notificationOrder = new()
            {
                Id = orderId,
                Creator = new(creator),
                Type = OrderType.Instant,
                Created = creationDateTime,
                SendersReference = senderReference,
                RequestedSendTime = creationDateTime,
                NotificationChannel = NotificationChannel.Sms,
                Templates =
                [
                    new SmsTemplate("Altinn", "Test message for wrong idempotency ID")
                ],
                Recipients =
                [
                    new Recipient([new SmsAddressPoint("+4799999999")])
                ]
            };

            var smsNotification = new SmsNotification
            {
                OrderId = orderId,
                Id = Guid.NewGuid(),
                Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
                SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
            };

            // Create the order in the database
            await orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, creationDateTime.AddMinutes(60), 1);

            // Act
            var result = await orderRepository.RetrieveTrackingInformation(creator, invalidIdempotencyId);

            // Assert
            Assert.Null(result);
        }
    }
}
