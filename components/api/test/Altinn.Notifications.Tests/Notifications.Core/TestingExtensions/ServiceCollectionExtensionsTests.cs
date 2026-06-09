using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCoreServices_NotificationConfigMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddCoreServices(config));

        // Assert
        Assert.Equal("config", exception.ParamName);
        Assert.StartsWith("Required NotificationConfig is missing from application configuration", exception.Message);
    }

    [Fact]
    public void AddCoreServices_ValidConfig_RegistersSmsAndEmailPublishBackgroundServices()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NotificationConfig:DefaultEmailFromAddress"] = "noreply@altinn.no"
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddCoreServices(config);

        // Assert
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(SmsPublishBackgroundService));

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(EmailPublishBackgroundService));
    }
}
