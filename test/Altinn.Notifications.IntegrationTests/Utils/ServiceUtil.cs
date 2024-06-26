﻿using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class ServiceUtil
{
    public static List<object> GetServices(List<Type> interfaceTypes, Dictionary<string, string>? envVariables = null)
    {
        if (envVariables != null)
        {
            foreach (var item in envVariables)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }

        var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json")
            .AddEnvironmentVariables();

        var config = builder.Build();

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddLogging();
        services.AddPostgresRepositories(config);
        services.AddCoreServices(config);
        services.AddKafkaServices(config);
        services.AddAltinnClients(config);
        services.AddAuthorizationService(config);

        var serviceProvider = services.BuildServiceProvider();
        List<object> outputServices = new();

        foreach (Type interfaceType in interfaceTypes)
        {
            var outputServiceObject = serviceProvider.GetServices(interfaceType)!;
            outputServices.AddRange(outputServiceObject!);
        }

        return outputServices;
    }
}
