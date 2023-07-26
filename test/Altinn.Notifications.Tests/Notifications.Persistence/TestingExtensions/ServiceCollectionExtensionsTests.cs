using System;

using Altinn.Notifications.Persistence.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingExtensions;

public class ServiceCollectionExtensionsTests
{

    [Fact]
    public void AddPostgresRepositories_PostgreSettings_ThrowsException()
    {
        Environment.SetEnvironmentVariable("PostgreSQLSettings_EnableDBConnection", "true");

        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();

        var config = builder.Build();

        IServiceCollection services = new ServiceCollection()
           .AddLogging();

        Assert.Throws<ArgumentNullException>(() => services.AddPostgresRepositories(config));
    }
}