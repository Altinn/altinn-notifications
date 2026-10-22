using System.Net;

using Altinn.Notifications.Email.Health;

using Microsoft.AspNetCore.Hosting;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTests.Endpoints;

public class OtlpExporterTests : IClassFixture<IntegrationTestWebApplicationFactory<HealthCheck>>
{
    private readonly IntegrationTestWebApplicationFactory<HealthCheck> _factory;

    public OtlpExporterTests(IntegrationTestWebApplicationFactory<HealthCheck> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Startup_WithOtlpEndpointConfigured_ReturnsHealthy()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"));
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
