using Altinn.Notifications.Sms.Integrations.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_MissingSmsGatewayConfig_ThrowsException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KafkaSettings__BrokerAddress", "localhost:9092", EnvironmentVariableTarget.Process);
        string expectedExceptionMessage = "Required SmsGatewayConfiguration settings is missing from application configuration. (Parameter 'config')";

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_MissingKafkaConfig_ThrowsException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KafkaSettings__BrokerAddress", null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("SmsGatewaySettings__Endpoint", "https://vg.no", EnvironmentVariableTarget.Process);
        string expectedExceptionMessage = "Required Kafka settings is missing from application configuration (Parameter 'config')";

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_SmsGatewayConfigIncluded_NoException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SmsGatewaySettings__Endpoint", "https://vg.no", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("KafkaSettings__BrokerAddress", "localhost:9092", EnvironmentVariableTarget.Process);
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Null(exception);
    }
}
