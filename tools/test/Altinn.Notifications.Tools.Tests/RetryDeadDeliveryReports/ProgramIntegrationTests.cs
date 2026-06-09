using System;
using System.Collections.Generic;

using Altinn.Notifications.Tools.RetryDeadDeliveryReports.EventGrid;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.RetryDeadDeliveryReports;

public class ProgramIntegrationTests
{
    [Fact]
    public void Program_ThrowsException_WhenEventGridBaseUrlIsEmpty()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        var inMemorySettings = new Dictionary<string, string>
        {
            { "PostgreSQLSettings:ConnectionString", "Host=localhost;Database=test" },
            { "EventGrid:BaseUrl", string.Empty }, // Empty BaseUrl
            { "EventGrid:AccessKey", "test-key" } 
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
