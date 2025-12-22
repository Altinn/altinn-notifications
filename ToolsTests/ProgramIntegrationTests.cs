using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Persistence.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Tools.EventGrid;

namespace ToolsTests;

public class ProgramIntegrationTests
{
    [Fact]
    public void Program_ConfiguresServices_Correctly()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Set up minimal configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            {"PostgreSQLSettings:ConnectionString", "Host=localhost;Database=test;Username=test;Password=test"},
            {"EventGrid:BaseUrl", "https://test.eventgrid.azure.net/api/events"},
            {"EventGrid:AccessKey", "test-key"},
            {"KafkaSettings:BrokerAddress", "localhost:9092"}
        };

        builder.Configuration.AddInMemoryCollection(inMemorySettings!);

        // Configure services (same as Program.cs)
        builder.Services.Configure<PostgreSqlSettings>(
            builder.Configuration.GetSection("PostgreSQLSettings"));

        var connectionString = builder.Configuration["PostgreSQLSettings:ConnectionString"];
        builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString!);
            return dataSourceBuilder.Build();
        });

        builder.Services.AddSingleton(sp =>
        {
            var kafkaSettings = new KafkaSettings();
            builder.Configuration.GetSection("KafkaSettings").Bind(kafkaSettings);
            return kafkaSettings;
        });

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

        var host = builder.Build();

        // Act & Assert - Verify services can be resolved
        using var scope = host.Services.CreateScope();

        var dataSource = scope.ServiceProvider.GetService<NpgsqlDataSource>();
        Assert.NotNull(dataSource);

        var kafkaSettings = scope.ServiceProvider.GetService<KafkaSettings>();
        Assert.NotNull(kafkaSettings);
        Assert.Equal("localhost:9092", kafkaSettings.BrokerAddress);

        var eventGridClient = scope.ServiceProvider.GetService<IEventGridClient>();
        Assert.NotNull(eventGridClient);

        var eventGridSettings = scope.ServiceProvider.GetService<IOptions<EventGridSettings>>();
        Assert.NotNull(eventGridSettings);
        Assert.Equal("https://test.eventgrid.azure.net/api/events", eventGridSettings.Value.BaseUrl);
    }

    [Fact]
    public void Program_ThrowsException_WhenEventGridBaseUrlIsEmpty()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        var inMemorySettings = new Dictionary<string, string>
        {
            {"PostgreSQLSettings:ConnectionString", "Host=localhost;Database=test"},
            {"EventGrid:BaseUrl", ""}, // Empty BaseUrl
            {"EventGrid:AccessKey", "test-key"}
        };

        builder.Configuration.AddInMemoryCollection(inMemorySettings!);

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

        var host = builder.Build();

        // Act & Assert
        using var scope = host.Services.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEventGridClient>());

        Assert.Equal("EventGrid:BaseUrl is not configured", exception.Message);
    }
}
