using System;

using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations;
public class SharedClientConfigTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SharedClientConfig_ParamsSetBySASLProperties(bool includeUsernameAndPassword)
    {
        // Arrange
        KafkaSettings settings = new()
        {
            BrokerAddress = "localhost:9092"
        };

        if (includeUsernameAndPassword)
        {
            settings.SaslUsername = "username";
            settings.SaslPassword = "password";
        }

        // Act
        var config = new SharedClientConfig(settings);

        // Assert
        if (includeUsernameAndPassword)
        {
            Assert.Equal(6, config.TopicSpecification.NumPartitions);
            Assert.NotNull(config.AdminClientConfig.SaslMechanism);
            Assert.Equal("username", config.ClientConfig.SaslUsername);
        }
        else
        {
            Assert.Null(config.AdminClientConfig.SaslMechanism);
            Assert.True(string.IsNullOrEmpty(config.ClientConfig.SaslUsername));
            Assert.Equal(1, config.TopicSpecification.NumPartitions);
        }
    }
}