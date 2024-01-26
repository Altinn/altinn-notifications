using Altinn.Notifications.Sms.Health;

namespace Altinn.Notifications.Sms.IntegrationTests.Endpoints;

public class HealthCheckTests : IClassFixture<IntegrationTestWebApplicationFactory<HealthCheck>>
{
    private readonly IntegrationTestWebApplicationFactory<HealthCheck> _factory;

    public HealthCheckTests(IntegrationTestWebApplicationFactory<HealthCheck> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Test()
    {
        // Arrange
        HttpClient httpClient = _factory.CreateClient();

        // Act
        HttpResponseMessage actual = await httpClient.GetAsync("/health");

        // Assert
        Assert.Equal(200, (int)actual.StatusCode);
    }
}
