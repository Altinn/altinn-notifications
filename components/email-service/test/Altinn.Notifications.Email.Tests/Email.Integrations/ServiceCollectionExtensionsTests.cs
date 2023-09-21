using Altinn.Notifications.Email.Integrations.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_KafkaSettingsMissing_ThrowsException()
    {
        // Arrange
        string expectedExceptionMessage = "Required Kafka settings is missing from application configuration (Parameter 'config')";

        Environment.SetEnvironmentVariable("KafkaSettings__ConsumerGroupId", null);
        Environment.SetEnvironmentVariable("CommunicationServicesSettings__Connectionstring", "value");

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddIntegrationServicess_NotificationOrderConfigMissing_ThrowsException()
    {
        // Arrange
        string expectedExceptionMessage = "Required communication services settings is missing from application configuration (Parameter 'config')";

        Environment.SetEnvironmentVariable("KafkaSettings__ConsumerGroupId", "value");
        Environment.SetEnvironmentVariable("CommunicationServicesSettings__Connectionstring", null);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(expectedExceptionMessage, exception.Message);
    }

    [Fact]
    public void AddKafkaHealthChecks_KafkaSettingsMissing_ThrowsException()
    {
        Environment.SetEnvironmentVariable("KafkaSettings", null);

        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddIntegrationHealthChecks(config));
    }
}
