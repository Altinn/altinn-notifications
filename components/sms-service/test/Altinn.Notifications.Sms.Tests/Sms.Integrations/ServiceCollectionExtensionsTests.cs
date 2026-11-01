using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.LinkMobility;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class ServiceCollectionExtensionsTests
{
    private static IHostEnvironment CreateHostEnvironment(string environmentName)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);
        return env.Object;
    }

    private static IConfiguration BuildConfig(string? logPayloads)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["SmsGatewaySettings:LogPayloads"] = logPayloads,
            })
            .Build();
    }

    [Fact]
    public void AddSmsGatewayServices_SmsGatewaySettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddSmsGatewayServices(config, CreateHostEnvironment("Production")));

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
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSmsGatewayServices(config, CreateHostEnvironment("Production")));

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
        var exception = Record.Exception(() => services.AddSmsGatewayServices(config, CreateHostEnvironment("Production")));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void AddSmsGatewayServices_LogPayloadsNotSet_IsOffAndRegistersNoPayloadLogging()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddSmsGatewayServices(BuildConfig(null), CreateHostEnvironment("Staging"));

        // Assert
        var settings = (SmsGatewaySettings)services.Single(d => d.ServiceType == typeof(SmsGatewaySettings)).ImplementationInstance!;
        Assert.Equal(PayloadLogMode.Off, settings.LogPayloads);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(PayloadLoggingHandler));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(PayloadLoggingWarning));
    }

    [Fact]
    public void AddSmsGatewayServices_LogPayloadsPlainInProduction_ThrowsInvalidOperationException()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSmsGatewayServices(BuildConfig("Plain"), CreateHostEnvironment("Production")));

        // Assert
        Assert.Contains("LogPayloads=Plain", exception.Message);
    }

    [Fact]
    public void AddSmsGatewayServices_LogPayloadsOffInProduction_NoException()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddSmsGatewayServices(BuildConfig("Off"), CreateHostEnvironment("Production")));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void AddSmsGatewayServices_LogPayloadsPlainInStaging_RegistersHandlerAndStartupWarning()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddSmsGatewayServices(BuildConfig("Plain"), CreateHostEnvironment("Staging"));

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(PayloadLoggingHandler));
        Assert.Contains(services, d => d.ImplementationType == typeof(PayloadLoggingWarning));
    }
}
