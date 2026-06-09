using System;

using Altinn.Notifications.Persistence.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
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
