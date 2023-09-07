﻿using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Core.Consumers;

public class EmailStatusConsumerTests : IDisposable
{
    private readonly string _statusUpdatedTopicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__EmailStatusUpdatedTopicName", _statusUpdatedTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_statusUpdatedTopicName}\"]" }
        };

        using EmailStatusConsumer consumerService = (EmailStatusConsumer)ServiceUtil
                                                    .GetServices(new List<Type>() { typeof(IHostedService) }, vars)
                                                    .First(s => s.GetType() == typeof(EmailStatusConsumer))!;

        (NotificationOrder Order, EmailNotification Notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(_sendersRef);

        SendOperationResult sendOperationResult = new()
        {
            NotificationId = Notification.Id,
            OperationId = Guid.NewGuid().ToString(),
            SendResult = EmailNotificationResultType.Succeeded
        };
        await KafkaUtil.PublishMessageOnTopic(_statusUpdatedTopicName, sendOperationResult.Serialize());


        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerService.StopAsync(CancellationToken.None);

        // Assert

        string emailNotificationStatus = await SelectEmailNotificationStatus(Notification.Id);
        Assert.Equal(emailNotificationStatus, EmailNotificationResultType.Succeeded.ToString());

    }

    public async void Dispose()
    {
        await Dispose(true);

        GC.SuppressFinalize(this);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        string sql = $"delete from notifications.orders where sendersreference = '{_sendersRef}'";

        await PostgreUtil.RunSql(sql);
        await KafkaUtil.DeleteTopicAsync(_statusUpdatedTopicName);
    }

    private async Task<string> SelectEmailNotificationStatus(Guid notificationId)
    {
        string sql = $"select result from notifications.emailnotifications where alternateid = '{notificationId}'";
        return await PostgreUtil.RunSqlReturnStringOutput(sql);
    }
}
