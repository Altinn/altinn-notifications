using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class SmsStatusConsumerTests
{
    [Collection("SmsStatusConsumer-Test1")]
    public class ConsumeInvalidMessage_Tests
    {
        [Fact]
        public async Task ConsumeInvalidMessage_ShouldNotUpdateStatus()
        {
            // Arrange
            string sendersRef = $"ref-{Guid.NewGuid()}";
            string statusUpdatedTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);

                Dictionary<string, string> kafkaSettings = new()
                {
                    { "KafkaSettings__SmsStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using SmsStatusConsumer smsStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<SmsStatusConsumer>()
                    .First();

                (_, SmsNotification smsNotification) =
                    await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersRef, simulateCronJob: true, simulateConsumers: true);

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, "Invalid-Delivery-Report");

                long processedOrderCount = -1;
                string observedSmsStatus = string.Empty;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedSmsStatus != SmsNotificationResultType.New.ToString())
                        {
                            observedSmsStatus = await GetSmsNotificationStatus(smsNotification.Id);
                        }

                        if (processedOrderCount != 1)
                        {
                            processedOrderCount = await CountOrdersByStatus(smsNotification.Id, OrderProcessingStatus.Processed);
                        }

                        return observedSmsStatus == SmsNotificationResultType.New.ToString() && processedOrderCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await smsStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, processedOrderCount);
                Assert.Equal(SmsNotificationResultType.New.ToString(), observedSmsStatus);
            }
            finally
            {
                await PostgreUtil.DeleteStatusFeedFromDb(sendersRef);
                await PostgreUtil.DeleteOrderFromDb(sendersRef);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    [Collection("SmsStatusConsumer-Test2")]
    public class ConsumeDeliveredStatus_Tests
    {
        [Fact]
        public async Task ConsumeDeliveredStatus_ShouldMarkOrderCompleted_WithStatusFeedEntry()
        {
            // Arrange
            string sendersRef = $"ref-{Guid.NewGuid()}";
            string statusUpdatedTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);

                Dictionary<string, string> kafkaSettings = new()
                {
                    { "KafkaSettings__SmsStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using SmsStatusConsumer smsStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<SmsStatusConsumer>()
                    .First();

                (NotificationOrder order, SmsNotification notification) =
                    await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersRef, simulateCronJob: true);

                SmsSendOperationResult sendOperationResult = new()
                {
                    NotificationId = notification.Id,
                    GatewayReference = Guid.NewGuid().ToString(),
                    SendResult = SmsNotificationResultType.Delivered
                };

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, sendOperationResult.Serialize());

                int statusFeedCount = -1;
                long completedOrderCount = -1;
                string observedSmsStatus = string.Empty;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedSmsStatus != SmsNotificationResultType.Delivered.ToString())
                        {
                            observedSmsStatus = await GetSmsNotificationStatus(notification.Id);
                        }

                        if (completedOrderCount != 1)
                        {
                            completedOrderCount = await CountOrdersByStatus(notification.Id, OrderProcessingStatus.Completed);
                        }

                        if (statusFeedCount != 1)
                        {
                            statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                        }

                        return observedSmsStatus == SmsNotificationResultType.Delivered.ToString() && completedOrderCount == 1 && statusFeedCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await smsStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, statusFeedCount);
                Assert.Equal(1, completedOrderCount);
                Assert.Equal(SmsNotificationResultType.Delivered.ToString(), observedSmsStatus);
            }
            finally
            {
                await PostgreUtil.DeleteStatusFeedFromDb(sendersRef);
                await PostgreUtil.DeleteOrderFromDb(sendersRef);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    [Collection("SmsStatusConsumer-Test3")]
    public class ConsumeAcceptedStatus_Tests
    {
        [Fact]
        public async Task ConsumeAcceptedStatus_ShouldMarkOrderProcessed_WithoutStatusFeedEntry()
        {
            // Arrange
            string sendersRef = $"ref-{Guid.NewGuid()}";
            string statusUpdatedTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);

                Dictionary<string, string> kafkaSettings = new()
                {
                    { "KafkaSettings__SmsStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using SmsStatusConsumer smsStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<SmsStatusConsumer>()
                    .First();

                (NotificationOrder notificationOrder, SmsNotification notification) =
                    await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersRef, simulateCronJob: true);

                SmsSendOperationResult sendOperationResult = new()
                {
                    NotificationId = notification.Id,
                    GatewayReference = Guid.NewGuid().ToString(),
                    SendResult = SmsNotificationResultType.Accepted
                };

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, sendOperationResult.Serialize());

                int statusFeedCount = -1;
                long processedOrderCount = -1;
                string observedSmsStatus = string.Empty;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedSmsStatus != SmsNotificationResultType.Accepted.ToString())
                        {
                            observedSmsStatus = await GetSmsNotificationStatus(notification.Id);
                        }

                        if (processedOrderCount != 1)
                        {
                            processedOrderCount = await CountOrdersByStatus(notification.Id, OrderProcessingStatus.Processed);
                        }

                        if (statusFeedCount != 0)
                        {
                            statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(notificationOrder.Id);
                        }

                        return observedSmsStatus == SmsNotificationResultType.Accepted.ToString() && processedOrderCount == 1 && statusFeedCount == 0;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await smsStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(0, statusFeedCount);
                Assert.Equal(1, processedOrderCount);
                Assert.Equal(SmsNotificationResultType.Accepted.ToString(), observedSmsStatus);
            }
            finally
            {
                await PostgreUtil.DeleteStatusFeedFromDb(sendersRef);
                await PostgreUtil.DeleteOrderFromDb(sendersRef);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    [Collection("SmsStatusConsumer-Test4")]
    public class ConsumeDeliveredStatus_WithNullSendersReference_Tests
    {
        [Fact]
        public async Task ConsumeDeliveredStatus_WithNullSendersReference_ShouldCreateStatusFeedEntry()
        {
            // Arrange
            string statusUpdatedTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);

                Dictionary<string, string> kafkaSettings = new()
                {
                    { "KafkaSettings__SmsStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using SmsStatusConsumer smsStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<SmsStatusConsumer>()
                    .First();

                (NotificationOrder order, SmsNotification notification) =
                    await PostgreUtil.PopulateDBWithOrderAndSmsNotification(forceSendersReferenceToBeNull: true, simulateCronJob: true);

                SmsSendOperationResult sendOperationResult = new()
                {
                    NotificationId = notification.Id,
                    GatewayReference = Guid.NewGuid().ToString(),
                    SendResult = SmsNotificationResultType.Delivered
                };

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, sendOperationResult.Serialize());

                int statusFeedCount = -1;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (statusFeedCount != 1)
                        {
                            statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                        }

                        return statusFeedCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await smsStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, statusFeedCount);
            }
            finally
            {
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    [Collection("SmsStatusConsumer-Test5")]
    public class ConsumeDeliveredStatus_ServiceThrows_Tests
    {
        [Theory]
        [InlineData(SendStatusIdentifierType.NotificationId)]
        [InlineData(SendStatusIdentifierType.GatewayReference)]
        public async Task ConsumeDeliveredStatus_ServiceThrows_ShouldPublishRetryMessage(SendStatusIdentifierType identifierType)
        {
            // Arrange
            string statusUpdatedTopicName = Guid.NewGuid().ToString();
            string statusUpdatedRetryTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);
                await KafkaUtil.CreateTopicAsync(statusUpdatedRetryTopicName);

                var kafkaOptions = Options.Create(new KafkaSettings
                {
                    BrokerAddress = "localhost:9092",
                    Producer = new ProducerSettings(),
                    SmsStatusUpdatedTopicName = statusUpdatedTopicName,
                    SmsStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });

                var producerMock = new Mock<IKafkaProducer>(MockBehavior.Loose);
                var smsServiceMock = new Mock<ISmsNotificationService>();
                smsServiceMock
                    .Setup(e => e.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
                    .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Sms, Guid.NewGuid().ToString(), identifierType));

                SmsSendOperationResult sendOperationResult = identifierType == SendStatusIdentifierType.NotificationId
                    ? new SmsSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = SmsNotificationResultType.Delivered }
                    : new SmsSendOperationResult { GatewayReference = Guid.NewGuid().ToString(), SendResult = SmsNotificationResultType.Delivered };

                string serializedDeliveryReport = sendOperationResult.Serialize();

                using SmsStatusConsumer smsStatusConsumer =
                    new(producerMock.Object, NullLogger<SmsStatusConsumer>.Instance, kafkaOptions, smsServiceMock.Object);

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, serializedDeliveryReport);

                bool messagePublishedToRetryTopic = false;
                await IntegrationTestUtil.EventuallyAsync(
                    () =>
                    {
                        try
                        {
                            producerMock.Verify(e => e.ProduceAsync(statusUpdatedRetryTopicName, It.Is<string>(e => IsExpectedRetryMessage(e, serializedDeliveryReport))), Times.Once);

                            messagePublishedToRetryTopic = true;

                            return messagePublishedToRetryTopic;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await smsStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.True(messagePublishedToRetryTopic);
            }
            finally
            {
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedRetryTopicName);
            }
        }

        private static bool IsExpectedRetryMessage(string message, string expectedSendOperationResult)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            try
            {
                var retry = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options);
                return retry?.SendOperationResult == expectedSendOperationResult;
            }
            catch
            {
                return false;
            }
        }
    }

    [Collection("SmsStatusConsumer-Test6")]
    public class ConsumeFailedStatus_Tests
    {
        [Theory]
        [InlineData(SmsNotificationResultType.Failed)]
        [InlineData(SmsNotificationResultType.Failed_Deleted)]
        [InlineData(SmsNotificationResultType.Failed_Expired)]
        [InlineData(SmsNotificationResultType.Failed_Rejected)]
        [InlineData(SmsNotificationResultType.Failed_Undelivered)]
        [InlineData(SmsNotificationResultType.Failed_BarredReceiver)]
        [InlineData(SmsNotificationResultType.Failed_InvalidRecipient)]
        [InlineData(SmsNotificationResultType.Failed_RecipientReserved)]
        [InlineData(SmsNotificationResultType.Failed_RecipientNotIdentified)]
        public async Task ConsumeFailedStatus_ShouldMarkOrderCompleted_WithStatusFeedEntry(SmsNotificationResultType resultType)
        {
            // Arrange
            string sendersRef = $"ref-{Guid.NewGuid()}";
            string statusUpdatedTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);

                Dictionary<string, string> kafkaSettings = new()
                {
                    { "KafkaSettings__SmsStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using SmsStatusConsumer smsStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<SmsStatusConsumer>()
                    .First();

                (NotificationOrder order, SmsNotification notification) =
                    await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersRef, simulateCronJob: true);

                SmsSendOperationResult sendOperationResult = new()
                {
                    NotificationId = notification.Id,
                    GatewayReference = Guid.NewGuid().ToString(),
                    SendResult = resultType
                };

                // Act
                await smsStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, sendOperationResult.Serialize());

                long completedCount = -1;
                int statusFeedCount = -1;
                string observedSmsStatus = string.Empty;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedSmsStatus != resultType.ToString())
                        {
                            observedSmsStatus = await GetSmsNotificationStatus(notification.Id);
                        }

                        if (completedCount != 1)
                        {
                            completedCount = await CountOrdersByStatus(notification.Id, OrderProcessingStatus.Completed);
                        }

                        if (statusFeedCount != 1)
                        {
                            statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(order.Id);
                        }

                        return observedSmsStatus == resultType.ToString() && completedCount == 1 && statusFeedCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await smsStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, completedCount);
                Assert.Equal(1, statusFeedCount);
                Assert.Equal(resultType.ToString(), observedSmsStatus);
            }
            finally
            {
                await PostgreUtil.DeleteStatusFeedFromDb(sendersRef);
                await PostgreUtil.DeleteOrderFromDb(sendersRef);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    // Shared helper methods
    private static async Task<string> GetSmsNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.smsnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }

    private static async Task<long> CountOrdersByStatus(Guid orderAlternateid, OrderProcessingStatus processingStatus)
    {
        string sql = $"SELECT count (1) FROM notifications.orders o join notifications.smsnotifications e on e._orderid = o._id where e.alternateid = '{orderAlternateid}' and o.processedstatus = '{processingStatus}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }
}
