using Altinn.Notifications.Email.Integrations.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntegrationServices_CommunicationServicesSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required communication services settings are missing from application configuration", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_EmailServiceAdminSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required email service admin settings are missing from application configuration", exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_BlobDownloadTimeoutInSecondsIsZero_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:BlobDownloadTimeoutInSeconds"] = "0"
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));
        Assert.Contains(nameof(EmailServiceAdminSettings.BlobDownloadTimeoutInSeconds), exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_BlobDownloadConcurrencyIsZero_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:BlobDownloadTimeoutInSeconds"] = "30",
                ["EmailServiceAdminSettings:BlobDownloadConcurrency"] = "0"
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));
        Assert.Contains(nameof(EmailServiceAdminSettings.BlobDownloadConcurrency), exception.Message);
    }
}
