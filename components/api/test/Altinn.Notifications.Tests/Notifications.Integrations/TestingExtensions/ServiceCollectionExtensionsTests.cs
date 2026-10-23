using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Integrations.SendCondition;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.TestingExtensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAuthorizationService_UseMaskinportenNotSet_RegistersMaskinportenForConditionClient()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddAuthorizationService(config);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IMaskinportenService));
    }

    [Fact]
    public void AddAuthorizationService_UseMaskinportenFalse_RegistersPlainConditionClient()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendConditionClient:UseMaskinporten"] = "false"
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddAuthorizationService(config);

        // Assert
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IMaskinportenService));
        Assert.IsType<SendConditionClient>(services.BuildServiceProvider().GetRequiredService<IConditionClient>());
    }
}
