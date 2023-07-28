using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Altinn.Notifications.IntegrationTests.Utils;

public static class ServiceUtil
{
    public static List<object> GetServices(List<Type> interfaceTypes)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json");

        var config = builder.Build();

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection()
            .AddLogging()
            .AddPostgresRepositories(config);

        var serviceProvider = services.BuildServiceProvider();
        List<object> outputServices = new();

        foreach (Type interfaceType in interfaceTypes)
        {
            object outputServiceObject = serviceProvider.GetServices(interfaceType).First()!;
            outputServices.Add(outputServiceObject);
        }

        return outputServices;
    }
}
