using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.LinkMobility;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSmsGatewayServices_SmsGatewaySettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddSmsGatewayServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void AddSmsGatewayServices_TimeoutInSecondsZero_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["SmsGatewaySettings:TimeoutInSeconds"] = "0",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSmsGatewayServices(config));

        // Assert
        Assert.Contains(nameof(SmsGatewaySettings.TimeoutInSeconds), exception.Message);
    }

    [Fact]
    public void AddSmsGatewayServices_ValidConfig_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddSmsGatewayServices(config));

        // Assert
        Assert.Null(exception);
    }
}
