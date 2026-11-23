using System;
using System.Linq;

using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPostgresHealthChecks_RegistersReadyCheckWithTimeout()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddPostgresHealthChecks();

        // Assert
        using ServiceProvider provider = services.BuildServiceProvider();
        var registration = Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);

        Assert.Equal("notifications_postgres_health_check", registration.Name);
        Assert.Equal([HealthCheckTags.Ready], registration.Tags);
        Assert.Equal(TimeSpan.FromSeconds(5), registration.Timeout);
    }

    [Fact]
    public void AddPostgresRepositories_PostgreSQLSettingsMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection()
           .AddLogging();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddPostgresRepositories(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required PostgreSQLSettings is missing from application configuration", exception.Message);
    }
}
