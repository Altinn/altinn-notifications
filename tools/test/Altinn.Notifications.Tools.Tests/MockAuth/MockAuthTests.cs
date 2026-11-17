using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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

    [Fact]
    public async Task GetEnterpriseToken_WithoutOrg_IsForTtd()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("mock", "mock");

        string token = await client.GetStringAsync("/api/GetEnterpriseToken?orgNumber=123456789&scopes=altinn:serviceowner/notifications.create", TestContext.Current.CancellationToken);

        var jwt = new JsonWebToken(token);
        Assert.Equal("ttd", jwt.GetClaim("urn:altinn:org").Value);
        Assert.Equal("123456789", jwt.GetClaim("urn:altinn:orgNumber").Value);
    }

    [Fact]
    public async Task Jwks_WithConfiguredSigningKey_PublishesThatKey_WithTheSameKeyIdEveryStart()
    {
        using var rsa = RSA.Create(2048);
        string signingKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        string expectedModulus = Base64UrlEncoder.Encode(rsa.ExportParameters(false).Modulus);

        string firstKid = await PublishedKeyId(signingKey, expectedModulus);
        string secondKid = await PublishedKeyId(signingKey, expectedModulus);

        Assert.Equal(firstKid, secondKid);
    }

    [Fact]
    public async Task Jwks_WithoutConfiguredSigningKey_MakesANewKeyEachStart()
    {
        using var first = factory.WithWebHostBuilder(_ => { });
        using var second = factory.WithWebHostBuilder(_ => { });

        string firstJwks = await first.CreateClient().GetStringAsync("/authentication/api/v1/openid/jwks", TestContext.Current.CancellationToken);
        string secondJwks = await second.CreateClient().GetStringAsync("/authentication/api/v1/openid/jwks", TestContext.Current.CancellationToken);

        Assert.NotEqual(firstJwks, secondJwks);
    }

    // Starts the issuer with signingKey, checks that its JWKS has that key, and returns the key id.
    private async Task<string> PublishedKeyId(string signingKey, string expectedModulus)
    {
        using var issuer = factory.WithWebHostBuilder(builder => builder.UseSetting("MockAuthSettings:SigningKey", signingKey));
        using var jwks = JsonDocument.Parse(await issuer.CreateClient().GetStringAsync("/authentication/api/v1/openid/jwks", TestContext.Current.CancellationToken));
        var key = Assert.Single(jwks.RootElement.GetProperty("keys").EnumerateArray());
        Assert.Equal(expectedModulus, key.GetProperty("n").GetString());
        return key.GetProperty("kid").GetString()!;
    }

    private static AuthenticationHeaderValue Basic(string username, string password)
    {
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
    }
}
