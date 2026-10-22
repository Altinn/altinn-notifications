using System.Net;

using Microsoft.AspNetCore.Hosting;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Telemetry;

public class OtlpExporterTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>
{
    private readonly IntegrationTestWebApplicationFactory<Program> _factory;

    public OtlpExporterTests(IntegrationTestWebApplicationFactory<Program> factory)
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
