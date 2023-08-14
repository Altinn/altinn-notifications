using System;

using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations;
public class SharedClientConfigTests : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    [Theory]
    // [InlineData("Production", true)]
    // [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public void SharedClientConfig_ParamsSetByEnvironment(string env, bool cloudParamsIncluded)
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", env);

        KafkaSettings settings = new KafkaSettings()
        {
            BrokerAddress = "localhost:9092",
            SaslPassword = "password",
            SaslUsername = "username"
        };

        // Act
        var config = new SharedClientConfig(settings);

        // Assert
        if (cloudParamsIncluded)
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
