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
        Environment.SetEnvironmentVariable("KafkaSettings__PastDueOrdersTopicName", null);
        Environment.SetEnvironmentVariable("NotificationConfig__DefaultEmailFromAddress", "value");

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));
    }

    [Fact]
    public void AddCoreServices_NotificationConfigMissing_ThrowsException()
    {
        Environment.SetEnvironmentVariable("KafkaSettings__PastDueOrdersTopicName", "value");
        Environment.SetEnvironmentVariable("NotificationConfig__DefaultEmailFromAddress", null);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));
    }
}
