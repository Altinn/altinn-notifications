using Altinn.Notifications.Sms.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Core;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_MissingKafkaConfig_ThrowsException()
    {
        // Arrange
        string expectedExceptionMessage = "Required Kafka settings is missing from application configuration (Parameter 'config')";

        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_SmsGatewayConfigIncluded_NoException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KafkaSettings__BrokerAddress", "localhost:9092", EnvironmentVariableTarget.Process);
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddCoreServices(config));

        // Assert
        Assert.Null(exception);
    }
}
