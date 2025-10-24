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

public class EmailStatusConsumerTests
{
    [Collection("EmailStatusConsumer-Test1")]
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
                    { "KafkaSettings__EmailStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using EmailStatusConsumer emailStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<EmailStatusConsumer>()
                    .First();

                (_, EmailNotification emailNotification) =
                    await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true, simulateConsumers: true);

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, "Invalid-Delivery-Report");

                long processedOrderCount = -1;
                string observedEmailStatus = string.Empty;

                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedEmailStatus != EmailNotificationResultType.New.ToString())
                        {
                            observedEmailStatus = await GetEmailNotificationStatus(emailNotification.Id);
                        }

                        if (processedOrderCount != 1)
                        {
                            processedOrderCount = await CountOrdersWithStatus(emailNotification.Id, OrderProcessingStatus.Processed);
                        }

                        return observedEmailStatus == EmailNotificationResultType.New.ToString() && processedOrderCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await emailStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, processedOrderCount);
                Assert.Equal(EmailNotificationResultType.New.ToString(), observedEmailStatus);
            }
            finally
            {
                await PostgreUtil.DeleteStatusFeedFromDb(sendersRef);
                await PostgreUtil.DeleteOrderFromDb(sendersRef);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    [Collection("EmailStatusConsumer-Test2")]
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
                    { "KafkaSettings__EmailStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using EmailStatusConsumer emailStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<EmailStatusConsumer>()
                    .First();

                (NotificationOrder notificationOrder, EmailNotification emailNotification) =
                    await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

                EmailSendOperationResult deliveryReport = new()
                {
                    NotificationId = emailNotification.Id,
                    OperationId = Guid.NewGuid().ToString(),
                    SendResult = EmailNotificationResultType.Delivered
                };

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, deliveryReport.Serialize());

                int statusFeedCount = -1;
                long completedOrderCount = -1;
                string observedEmailStatus = string.Empty;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedEmailStatus != EmailNotificationResultType.Delivered.ToString())
                        {
                            observedEmailStatus = await GetEmailNotificationStatus(emailNotification.Id);
                        }

                        if (statusFeedCount != 1)
                        {
                            statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(notificationOrder.Id);
                        }

                        if (completedOrderCount != 1)
                        {
                            completedOrderCount = await CountOrdersWithStatus(emailNotification.Id, OrderProcessingStatus.Completed);
                        }

                        return observedEmailStatus == EmailNotificationResultType.Delivered.ToString() && statusFeedCount == 1 && completedOrderCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await emailStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, statusFeedCount);
                Assert.Equal(1, completedOrderCount);
                Assert.Equal(EmailNotificationResultType.Delivered.ToString(), observedEmailStatus);
            }
            finally
            {
                await PostgreUtil.DeleteStatusFeedFromDb(sendersRef);
                await PostgreUtil.DeleteOrderFromDb(sendersRef);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    [Collection("EmailStatusConsumer-Test3")]
    public class ConsumeSucceededStatus_Tests
    {
        [Fact]
        public async Task ConsumeSucceededStatus_ShouldMarkOrderProcessed_WithoutStatusFeedEntry()
        {
            // Arrange
            string sendersRef = $"ref-{Guid.NewGuid()}";
            string statusUpdatedTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);

                Dictionary<string, string> kafkaSettings = new()
                {
                    { "KafkaSettings__EmailStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using EmailStatusConsumer emailStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<EmailStatusConsumer>()
                    .First();

                (NotificationOrder notificationOrder, EmailNotification emailNotification) =
                    await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

                EmailSendOperationResult deliveryReport = new()
                {
                    NotificationId = emailNotification.Id,
                    OperationId = Guid.NewGuid().ToString(),
                    SendResult = EmailNotificationResultType.Succeeded
                };

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, deliveryReport.Serialize());

                int statusFeedCount = -1;
                long processedOrderCount = -1;
                string observedEmailStatus = string.Empty;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedEmailStatus != EmailNotificationResultType.Succeeded.ToString())
                        {
                            observedEmailStatus = await GetEmailNotificationStatus(emailNotification.Id);
                        }

                        if (statusFeedCount != 0)
                        {
                            statusFeedCount = await PostgreUtil.SelectStatusFeedEntryCount(notificationOrder.Id);
                        }

                        if (processedOrderCount != 1)
                        {
                            processedOrderCount = await CountOrdersWithStatus(emailNotification.Id, OrderProcessingStatus.Processed);
                        }

                        return observedEmailStatus == EmailNotificationResultType.Succeeded.ToString() && statusFeedCount == 0 && processedOrderCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await emailStatusConsumer.StopAsync(CancellationToken.None);

                // Assert using captured values
                Assert.Equal(0, statusFeedCount);
                Assert.Equal(1, processedOrderCount);
                Assert.Equal(EmailNotificationResultType.Succeeded.ToString(), observedEmailStatus);
            }
            finally
            {
                await PostgreUtil.DeleteStatusFeedFromDb(sendersRef);
                await PostgreUtil.DeleteOrderFromDb(sendersRef);
                await KafkaUtil.DeleteTopicAsync(statusUpdatedTopicName);
            }
        }
    }

    [Collection("EmailStatusConsumer-Test4")]
    public class ConsumeDeliveredStatus_ServiceThrows_Tests
    {
        [Theory]
        [InlineData(SendStatusIdentifierType.OperationId)]
        [InlineData(SendStatusIdentifierType.NotificationId)]
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
                    EmailStatusUpdatedTopicName = statusUpdatedTopicName,
                    EmailStatusUpdatedRetryTopicName = statusUpdatedRetryTopicName,
                    Consumer = new ConsumerSettings { GroupId = $"altinn-notifications-{Guid.NewGuid():N}" }
                });

                var producerMock = new Mock<IKafkaProducer>(MockBehavior.Loose);
                var emailServiceMock = new Mock<IEmailNotificationService>();
                emailServiceMock
                    .Setup(e => e.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
                    .ThrowsAsync(new SendStatusUpdateException(NotificationChannel.Email, Guid.NewGuid().ToString(), identifierType));

                EmailSendOperationResult deliveryReport = identifierType == SendStatusIdentifierType.NotificationId
                    ? new EmailSendOperationResult { NotificationId = Guid.NewGuid(), SendResult = EmailNotificationResultType.Delivered }
                    : new EmailSendOperationResult { OperationId = Guid.NewGuid().ToString(), SendResult = EmailNotificationResultType.Delivered };

                string serializedDeliveryReport = deliveryReport.Serialize();

                using EmailStatusConsumer emailStatusConsumer =
                    new(producerMock.Object, NullLogger<EmailStatusConsumer>.Instance, kafkaOptions, emailServiceMock.Object);

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
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

                await emailStatusConsumer.StopAsync(CancellationToken.None);

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

    [Collection("EmailStatusConsumer-Test5")]
    public class ConsumeFailedStatus_Tests
    {
        [Theory]
        [InlineData(EmailNotificationResultType.Failed)]
        [InlineData(EmailNotificationResultType.Failed_Bounced)]
        [InlineData(EmailNotificationResultType.Failed_Quarantined)]
        [InlineData(EmailNotificationResultType.Failed_FilteredSpam)]
        [InlineData(EmailNotificationResultType.Failed_RecipientReserved)]
        [InlineData(EmailNotificationResultType.Failed_InvalidEmailFormat)]
        [InlineData(EmailNotificationResultType.Failed_SupressedRecipient)]
        [InlineData(EmailNotificationResultType.Failed_RecipientNotIdentified)]
        public async Task ConsumeFailedStatus_ShouldMarkOrderCompleted_WithStatusFeedEntry(EmailNotificationResultType resultType)
        {
            // Arrange
            string sendersRef = $"ref-{Guid.NewGuid()}";
            string statusUpdatedTopicName = Guid.NewGuid().ToString();

            try
            {
                await KafkaUtil.CreateTopicAsync(statusUpdatedTopicName);

                Dictionary<string, string> kafkaSettings = new()
                {
                    { "KafkaSettings__EmailStatusUpdatedTopicName", statusUpdatedTopicName },
                    { "KafkaSettings__Admin__TopicList", $"[\"{statusUpdatedTopicName}\"]" }
                };

                using EmailStatusConsumer emailStatusConsumer = ServiceUtil
                    .GetServices([typeof(IHostedService)], kafkaSettings)
                    .OfType<EmailStatusConsumer>()
                    .First();

                (_, EmailNotification notification) =
                    await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersRef, simulateCronJob: true);

                EmailSendOperationResult deliveryReport = new()
                {
                    SendResult = resultType,
                    NotificationId = notification.Id,
                    OperationId = Guid.NewGuid().ToString()
                };

                // Act
                await emailStatusConsumer.StartAsync(CancellationToken.None);
                await KafkaUtil.PublishMessageOnTopic(statusUpdatedTopicName, deliveryReport.Serialize());

                long completedOrdersCount = -1;
                string observedEmailStatus = string.Empty;
                await IntegrationTestUtil.EventuallyAsync(
                    async () =>
                    {
                        if (observedEmailStatus != resultType.ToString())
                        {
                            observedEmailStatus = await GetEmailNotificationStatus(notification.Id);
                        }

                        if (completedOrdersCount != 1)
                        {
                            completedOrdersCount = await CountOrdersWithStatus(notification.Id, OrderProcessingStatus.Completed);
                        }

                        return observedEmailStatus == resultType.ToString() && completedOrdersCount == 1;
                    },
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(100));

                await emailStatusConsumer.StopAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, completedOrdersCount);
                Assert.Equal(resultType.ToString(), observedEmailStatus);
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
    private static async Task<string> GetEmailNotificationStatus(Guid emailNotificationAlternateid)
    {
        string sql = $"select result from notifications.emailnotifications where alternateid = '{emailNotificationAlternateid}'";
        return await PostgreUtil.RunSqlReturnOutput<string>(sql);
    }

    private static async Task<long> CountOrdersWithStatus(Guid orderAlternateid, OrderProcessingStatus orderProcessingStatus)
    {
        string sql = $"SELECT count (1) FROM notifications.orders o join notifications.emailnotifications e on e._orderid = o._id where e.alternateid = '{orderAlternateid}' and o.processedstatus = '{orderProcessingStatus}'";
        return await PostgreUtil.RunSqlReturnOutput<long>(sql);
    }
}
