using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.AltinnServiceUpdate;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingConsumers;

public class AltinnServiceUpdateConsumerTests : IAsyncLifetime
{
    private static readonly string _serviceUpdateTopicName = Guid.NewGuid().ToString();

    /// <summary>
    /// Scenario: A new service update for Notifications email with a resource limit exceeded message
    /// Expected Side effect: resourcelimitlog table is populated with a datetime value for when the resource limit will be reset
    /// </summary>
    [Fact]
    public async Task RunTask_ConfirmExpectedSideEffects()
    {
        // Arrange
        Dictionary<string, string> vars = new()
        {
            { "KafkaSettings__AltinnServiceUpdateTopicName", _serviceUpdateTopicName },
            { "KafkaSettings__Admin__TopicList", $"[\"{_serviceUpdateTopicName}\"]" }
        };

        using AltinnServiceUpdateConsumer consumerService = (AltinnServiceUpdateConsumer)ServiceUtil
                                                    .GetServices(new List<Type>() { typeof(IHostedService) }, vars)
                                                    .First(s => s.GetType() == typeof(AltinnServiceUpdateConsumer))!;

        ResourceLimitExceeded data = new()
        {
            ResetTime = DateTime.UtcNow.AddMinutes(5),
            Resource = "Azure Communication Services",
        };

        GenericServiceUpdate serviceUpdate = new()
        {
            Source = "platform-notifications-email",
            Schema = AltinnServiceUpdateSchema.ResourceLimitExceeded,
            Data = data.Serialize()
        };

        await KafkaUtil.PublishMessageOnTopic(_serviceUpdateTopicName, serviceUpdate.Serialize());

        // Act
        await consumerService.StartAsync(CancellationToken.None);
        await Task.Delay(10000);
        await consumerService.StopAsync(CancellationToken.None);

        // Assert
        DateTime? actualTimeout = await SelectEmailResourceLimitFromDb();
        Assert.NotNull(actualTimeout);
        Assert.True(actualTimeout > DateTime.MinValue);
    }

    private static async Task<DateTime?> SelectEmailResourceLimitFromDb()
    {
        string sql = @"SELECT emaillimittimeout
	                   FROM notifications.resourcelimitlog
	                   order by id desc
	                   limit 1;";

        return await PostgreUtil.RunSqlReturnOutput<DateTime?>(sql);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        string sql = @"UPDATE notifications.resourcelimitlog
                        SET emaillimittimeout = NULL
                        WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog)";

        await PostgreUtil.RunSql(sql);
    }
}
