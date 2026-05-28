using Altinn.Notifications.Sms.Integrations.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_KafkaSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void AddIntegrationServices_SmsGatewaySettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void AddIntegrationServices_ValidConfig_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BrokerAddress"] = "localhost:9092",
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Null(exception);
    }
}
