using System;
using Altinn.Notifications.Integrations.Configuration;
using Tools;
using Xunit;

namespace ToolsTests;

public class SharedClientConfigTests
{
    [Fact]
    public void DefaultSettings_Produces_DefaultTopicSpecification()
    {
        var kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092"
        };

        var sc = new SharedClientConfig(kafkaSettings);

        Assert.Equal(1, sc.TopicSpecification.NumPartitions);
        Assert.Equal(1, sc.TopicSpecification.ReplicationFactor);
        Assert.Null(sc.TopicSpecification.Configs);
    }

    [Fact]
    public void WithAdminCredentials_Updates_TopicSpecification()
    {
        var kafkaSettings = new KafkaSettings
        {
            BrokerAddress = "localhost:9092",
            Admin = new AdminSettings
            {
                SaslUsername = "user",
                SaslPassword = "pwd",
                RetentionTime = 10
            }
        };

        var sc = new SharedClientConfig(kafkaSettings);

        Assert.Equal(6, sc.TopicSpecification.NumPartitions);
        Assert.Equal(3, sc.TopicSpecification.ReplicationFactor);
        Assert.NotNull(sc.TopicSpecification.Configs);
        Assert.True(sc.TopicSpecification.Configs.ContainsKey("cleanup.policy"));
    }
}
