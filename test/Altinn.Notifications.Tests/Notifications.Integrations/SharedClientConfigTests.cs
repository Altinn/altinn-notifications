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
            settings.Admin.SaslUsername = "username";
            settings.Admin.SaslPassword = "password";
        }

        // Act
        var config = new SharedClientConfig(settings);

        // Assert
        if (includeUsernameAndPassword)
        {
            Assert.Equal(6, config.TopicSpecification.NumPartitions);
            Assert.NotNull(config.AdminClientSettings.SaslMechanism);
            Assert.Equal("username", config.AdminClientSettings.SaslUsername);
        }
        else
        {
            Assert.Null(config.AdminClientSettings.SaslMechanism);
            Assert.True(string.IsNullOrEmpty(config.AdminClientSettings.SaslUsername));
            Assert.Equal(1, config.TopicSpecification.NumPartitions);
        }
    }
}