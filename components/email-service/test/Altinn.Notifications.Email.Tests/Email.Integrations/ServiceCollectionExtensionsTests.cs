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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButStatusCheckQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EmailStatusCheckQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("EmailStatusCheckQueueName must be configured.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButSendResultQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EmailSendResultQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal("EmailSendResultQueueName must be configured.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void AddIntegrationServices_WolverineEnabledButRateLimitQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = queueName,
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Equal(
            "EmailServiceRateLimitQueueName must be configured.",
            exception.Message);
    }

    [Fact]
    public void AddIntegrationServices_WolverineEnabledWithAllPublishers_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",    
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Null(exception);
    }
}
