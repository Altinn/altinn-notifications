using System;

using Altinn.Notifications.Integrations.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.TestingExtensions;

public class ServiceCollectionExtensionsTests
{

    [Fact]
    public void AddKafkaServices_KafkaSettingsMissing_ThrowsException()
    {
        Environment.SetEnvironmentVariable("KafkaSettings", null);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddKafkaServices(config));
    }

    [Fact]
    public void AddKafkaHealthChecks_KafkaSettingsMissing_ThrowsException()
    {
        Environment.SetEnvironmentVariable("KafkaSettings", null);

        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection()
           .AddLogging();

        Assert.Throws<ArgumentNullException>(() => services.AddKafkaHealthChecks(config));
    }
}