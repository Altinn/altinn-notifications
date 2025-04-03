using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
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
        public async Task Create_NotificationOrderChainWithEmailRecipientWithoutReminders_PersistsOrdersChainAndOrderAndEmailTemplate()
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
                            SenderName = "Email sender name",
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

            string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}'";
            int ordersCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
            Assert.Equal(1, ordersCount);

            string emailTextSql = $@"SELECT count(*) FROM notifications.emailtexts as et JOIN notifications.orders o ON et._orderid = o._id WHERE o.alternateid = '{orderId}'";
            int emailTextCount = await PostgreUtil.RunSqlReturnOutput<int>(emailTextSql);
            Assert.Equal(1, emailTextCount);
        }

        [Fact]
        public async Task Create_NotificationOrderChainWithSmsRecipientWithReminders_PersistsOrdersChainAndOrderAndEmailTemplate()
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

            string mainOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{mainOrderId}'";
            string mainOrdersChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            string firstReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstReminderOrderId}'";
            string secondReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondReminderOrderId}'";
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
        public async Task Create_NotificationOrderChainWithPersonRecipientAndReminders_PersistsOrdersChainAndOrdersAndTemplates()
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
                            SenderName = "Main email sender name",
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
                                    SenderName = "First reminder email sender",
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
                                    SenderName = "Second reminder email sender",
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

            string mainOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{mainOrderId}'";
            string mainOrdersChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            string firstReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstReminderOrderId}'";
            string secondReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondReminderOrderId}'";

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
        public async Task Create_NotificationOrderChainWithOrganizationRecipientAndReminders_PersistsOrdersChainAndOrdersAndTemplates()
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
                            SenderName = "Main email sender name",
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
                                    SenderName = "First reminder email sender",
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
                                    SenderName = "Second reminder email sender",
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

            string mainOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{mainOrderId}'";
            string mainOrdersChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
            string firstReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstReminderOrderId}'";
            string secondReminderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondReminderOrderId}'";

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
        public async Task Create_NotificationOrderChainWithEmailRecipientWithoutReminders_WhenCancellationRequested_ThrowsOperationCanceledException()
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
                            SenderName = "Email sender name",
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
                RequestedSendTime = requestedSendTime,
                NotificationChannel = NotificationChannel.Email,
                SendersReference = "CANCELLATION-TEST-REF",
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
        public async Task Create_NotificationOrderChainWithEmailRecipientWithoutReminders_WithInvalidTemplates_ThrowsNullReferenceException()
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
                .SetRequestedSendTime(requestedSendTime)
                .SetIdempotencyId("EXCEPTION-TEST-ID")
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
                            SenderName = "Email sender name",
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
    }
}
