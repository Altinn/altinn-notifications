using System;

using Altinn.Notifications.Core.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingExtensions;
public class ServiceCollectionExtensionsTests
{

    [Fact]
    public void AddCoreServices_KafkaSettingsMissing_ThrowsException()
    {
        Environment.SetEnvironmentVariable("NotificationOrderConfig__DefaultEmailFromAddress", "noreply@altinn.no");

        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();
       
        var config = builder.Build();

        IServiceCollection services = new ServiceCollection()
           .AddLogging();   

        Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));
    }

    [Fact]
    public void AddCoreServices_NotificationOrderConfig_ThrowsException()
    {
        Environment.SetEnvironmentVariable("KafkaSettings__PastDueOrdersTopicName", "value");

        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();

        var config = builder.Build();

        IServiceCollection services = new ServiceCollection()
           .AddLogging();

        Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));
    }
}
