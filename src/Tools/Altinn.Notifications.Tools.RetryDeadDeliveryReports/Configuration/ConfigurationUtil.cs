using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Repository;
using Altinn.Notifications.Tools.RetryDeadDeliveryReports.EventGrid;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Tools.RetryDeadDeliveryReports.Configuration;

[ExcludeFromCodeCoverage]
internal static class ConfigurationUtil
{
    internal static void ConfigureServices(HostApplicationBuilder builder)
    {
        ConfigureAppSettings(builder);
        ConfigureDatabase(builder);
        ConfigureKafka(builder);
        ConfigureEventGrid(builder);
        ConfigureProcessingSettings(builder);
        RegisterRepositoriesAndServices(builder);
    }

    internal static void ConfigureAppSettings(HostApplicationBuilder builder)
    {
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<Program>(optional: true);
    }

    internal static void ConfigureDatabase(HostApplicationBuilder builder)
    {
        builder.Services.Configure<PostgreSqlSettings>(
            builder.Configuration.GetSection("PostgreSQLSettings"));

        var connectionString = builder.Configuration["PostgreSQLSettings:ConnectionString"];
        var notificationsDbPwd = builder.Configuration["PostgreSQLSettings:NotificationsDbPwd"];

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(notificationsDbPwd))
        {
            throw new InvalidOperationException("PostgreSQLSettings:ConnectionString is not configured");
        }
        
        var credentials = string.Format(connectionString, notificationsDbPwd);

        builder.Services.AddSingleton(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(credentials);
            return dataSourceBuilder.Build();
        });
    }

    internal static void ConfigureKafka(HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(sp =>
        {
            var kafkaSettings = new KafkaSettings();
            builder.Configuration.GetSection("KafkaSettings").Bind(kafkaSettings);
            return kafkaSettings;
        });
    }

    internal static void ConfigureEventGrid(HostApplicationBuilder builder)
    {
        builder.Services.Configure<EventGridSettings>(builder.Configuration.GetSection("EventGrid"));
        builder.Services.AddHttpClient<IEventGridClient, EventGridClient>((sp, client) =>
        {
            var cfg = sp.GetRequiredService<IOptions<EventGridSettings>>().Value;
            if (string.IsNullOrWhiteSpace(cfg.BaseUrl))
            {
                throw new InvalidOperationException("EventGrid:BaseUrl is not configured");
            }

            client.BaseAddress = new Uri(cfg.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    internal static void ConfigureProcessingSettings(HostApplicationBuilder builder)
    {
        builder.Services.Configure<ProcessingSettings>(
            builder.Configuration.GetSection("ProcessingSettings"));
    }

    internal static void RegisterRepositoriesAndServices(HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IDeadDeliveryReportRepository, DeadDeliveryReportRepository>();
    }
}
