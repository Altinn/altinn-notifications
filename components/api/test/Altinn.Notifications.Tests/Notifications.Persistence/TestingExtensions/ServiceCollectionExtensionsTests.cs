using System;

using Altinn.Notifications.Persistence.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Persistence.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPostgresRepositories_PostgreSettingsMissing_ThrowsException()
    {
        Environment.SetEnvironmentVariable("PostgreSettings", null);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection()
           .AddLogging();

        Assert.Throws<ArgumentNullException>(() => services.AddPostgresRepositories(config));
    }
}
