using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.MockAuth.Configuration;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.MockAuth;

public class MockAuthTests(WebApplicationFactory<MockAuthSettings> factory) : IClassFixture<WebApplicationFactory<MockAuthSettings>>
{
    private const string TokenPath = "/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create";

    [Fact]
    public async Task GetEnterpriseToken_WithoutBasicAuth_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(TokenPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEnterpriseToken_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("mock", "wrong");

        using var response = await client.GetAsync(TokenPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEnterpriseToken_ValidatesAgainstPublishedJwks_WithOrgAndScopeClaims()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("mock", "mock");

        string token = await client.GetStringAsync(TokenPath, TestContext.Current.CancellationToken);

        // Follow the same discovery path as the API's JwtCookie handler.
        using var discovery = JsonDocument.Parse(await client.GetStringAsync("/authentication/api/v1/openid/.well-known/openid-configuration", TestContext.Current.CancellationToken));
        string jwksUri = discovery.RootElement.GetProperty("jwks_uri").GetString()!;
        var jwks = new JsonWebKeySet(await client.GetStringAsync(jwksUri, TestContext.Current.CancellationToken));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireExpirationTime = true,
            ValidateLifetime = true,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("ttd", result.ClaimsIdentity.FindFirst("urn:altinn:org")?.Value);
        Assert.Equal("altinn:serviceowner/notifications.create", result.ClaimsIdentity.FindFirst("urn:altinn:scope")?.Value);
        Assert.Equal("991825827", result.ClaimsIdentity.FindFirst("urn:altinn:orgNumber")?.Value);
    }

    [Fact]
    public async Task GetEnterpriseToken_WithCommaSeparatedScopes_ReturnsSpaceSeparatedScopeClaim()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("mock", "mock");

        string token = await client.GetStringAsync(
            "/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create,altinn:serviceowner/notifications.composedemail.create",
            TestContext.Current.CancellationToken);

        var jwt = new JsonWebToken(token);
        Assert.Equal("altinn:serviceowner/notifications.create altinn:serviceowner/notifications.composedemail.create", jwt.GetClaim("urn:altinn:scope").Value);
    }

    private static AuthenticationHeaderValue Basic(string username, string password)
    {
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
    }
}
