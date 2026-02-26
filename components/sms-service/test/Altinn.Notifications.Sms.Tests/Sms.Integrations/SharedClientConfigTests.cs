using Altinn.Notifications.Sms.Integrations.Configuration;

using Xunit;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

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
            Assert.Equal(3, config.TopicSpecification.ReplicationFactor);
            Assert.NotNull(config.AdminClientConfig.SaslMechanism);
            Assert.Equal("password", config.AdminClientConfig.SaslPassword);
        }
        else
        {
            Assert.Null(config.AdminClientConfig.SaslMechanism);
            Assert.True(string.IsNullOrEmpty(config.ProducerConfig.SaslUsername));
            Assert.Equal(1, config.TopicSpecification.NumPartitions);
        }
    }
}
