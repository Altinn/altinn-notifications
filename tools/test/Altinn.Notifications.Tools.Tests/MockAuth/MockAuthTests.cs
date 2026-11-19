using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Common.PEP.Authorization;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Tools.MockAuth;
using Altinn.Notifications.Tools.MockAuth.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.MockAuth;

public class MockAuthTests(WebApplicationFactory<MockAuthSettings> factory) : IClassFixture<WebApplicationFactory<MockAuthSettings>>
{
    private const string TokenPath = "/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create";
    private const string BothCreateScopesPath = "/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create,altinn:serviceowner/notifications.composedemail.create";
    private const string DashboardTokenPath = "/api/GetEnterpriseToken?org=ttd&scopes=altinn:notifications.support.admin";
    private const string PersonalTokenPath = "/api/GetPersonalToken?userId=20000000&partyId=50000000&pid=12345678901&scopes=altinn:portal/enduser digdir:dialogporten.noconsent";

    [Theory]
    [InlineData(TokenPath)]
    [InlineData(PersonalTokenPath)]
    public async Task TokenEndpoints_WithoutBasicAuth_ReturnUnauthorized(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(TokenPath)]
    [InlineData(PersonalTokenPath)]
    public async Task TokenEndpoints_WithWrongPassword_ReturnUnauthorized(string path)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("mock", "wrong");

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEnterpriseToken_ValidatesAgainstPublishedJwks_WithOrgAndScopeClaims()
    {
        using var client = AuthenticatedClient();

        var principal = await ValidateAsync(client, await client.GetStringAsync(TokenPath, TestContext.Current.CancellationToken));

        Assert.Equal("ttd", principal.FindFirst("urn:altinn:org")?.Value);
        Assert.Equal("altinn:serviceowner/notifications.create", principal.FindFirst("urn:altinn:scope")?.Value);
        Assert.Equal("991825827", principal.FindFirst("urn:altinn:orgNumber")?.Value);
        Assert.Equal("3", principal.FindFirst("urn:altinn:authlevel")?.Value);
    }

    [Fact]
    public async Task GetPersonalToken_ValidatesAgainstPublishedJwks_WithUserClaims()
    {
        using var client = AuthenticatedClient();

        var principal = await ValidateAsync(client, await client.GetStringAsync(PersonalTokenPath + "&authLvl=2", TestContext.Current.CancellationToken));

        Assert.Equal("20000000", principal.FindFirst("urn:altinn:userid")?.Value);
        Assert.Equal("50000000", principal.FindFirst("urn:altinn:partyid")?.Value);
        Assert.Equal("12345678901", principal.FindFirst("pid")?.Value);
        Assert.Equal("2", principal.FindFirst("urn:altinn:authlevel")?.Value);
        Assert.Equal("altinn:portal/enduser digdir:dialogporten.noconsent", principal.FindFirst("scope")?.Value);
        Assert.Null(principal.FindFirst("urn:altinn:org"));
    }

    [Fact]
    public async Task GetPersonalToken_WithoutParameters_GivesAUserWithDefaults()
    {
        using var client = AuthenticatedClient();

        var principal = await ValidateAsync(client, await client.GetStringAsync("/api/GetPersonalToken", TestContext.Current.CancellationToken));

        Assert.NotNull(principal.FindFirst("urn:altinn:userid"));
        Assert.NotNull(principal.FindFirst("urn:altinn:partyid"));
        Assert.Equal("3", principal.FindFirst("urn:altinn:authlevel")?.Value);
        Assert.Null(principal.FindFirst("scope"));
    }

    // The scope each policy in the API's AddAuthorizationRulesAndHandlers requires, checked by the API's own scope handler.
    [Theory]
    [InlineData(BothCreateScopesPath, AuthorizationConstants.SCOPE_NOTIFICATIONS_CREATE)]
    [InlineData(BothCreateScopesPath, AuthorizationConstants.SCOPE_NOTIFICATIONS_COMPOSED_EMAIL_CREATE)]
    [InlineData(DashboardTokenPath, AuthorizationConstants.SCOPE_SUPPORT_DASHBOARD_ACCESS)]
    [InlineData(PersonalTokenPath, "altinn:portal/enduser")]
    public async Task Tokens_PassTheApisScopeCheck(string path, string requiredScope)
    {
        using var client = AuthenticatedClient();
        var principal = await ValidateAsync(client, await client.GetStringAsync(path, TestContext.Current.CancellationToken));

        Assert.True(await HasScopeAsync(principal, requiredScope));
    }

    [Theory]
    [InlineData(TokenPath, AuthorizationConstants.SCOPE_NOTIFICATIONS_COMPOSED_EMAIL_CREATE)]
    [InlineData(TokenPath, "altinn:portal/enduser")]
    [InlineData(PersonalTokenPath, AuthorizationConstants.SCOPE_SUPPORT_DASHBOARD_ACCESS)]
    public async Task Tokens_WithoutTheScope_FailTheApisScopeCheck(string path, string requiredScope)
    {
        using var client = AuthenticatedClient();
        var principal = await ValidateAsync(client, await client.GetStringAsync(path, TestContext.Current.CancellationToken));

        Assert.False(await HasScopeAsync(principal, requiredScope));
    }

    [Theory]
    [InlineData("a,b", "a b")]
    [InlineData("a b", "a b")]
    [InlineData(" a, b ,c ", "a b c")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeScopes_SplitsOnCommasAndSpaces(string? scopes, string? expected)
    {
        Assert.Equal(expected, MockTokenIssuer.NormalizeScopes(scopes));
    }

    private HttpClient AuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("mock", "mock");
        return client;
    }

    /// <summary>
    /// Validates the way the API's JwtCookie handler does: keys from the discovered JWKS, and the same handler and parameters.
    /// </summary>
    private static async Task<ClaimsPrincipal> ValidateAsync(HttpClient client, string token)
    {
        using var discovery = JsonDocument.Parse(await client.GetStringAsync("/authentication/api/v1/openid/.well-known/openid-configuration", TestContext.Current.CancellationToken));
        string jwksUri = discovery.RootElement.GetProperty("jwks_uri").GetString()!;
        var jwks = new JsonWebKeySet(await client.GetStringAsync(jwksUri, TestContext.Current.CancellationToken));

        return new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                IssuerSigningKeys = jwks.GetSigningKeys(),
                ValidateIssuerSigningKey = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            },
            out _);
    }

    private static async Task<bool> HasScopeAsync(ClaimsPrincipal principal, string scope)
    {
        var requirement = new ScopeAccessRequirement(scope);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await new ScopeAccessHandler().HandleAsync(context);

        return context.HasSucceeded;
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
