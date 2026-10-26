using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Handlers;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Integrations.SendCondition;
using Altinn.Notifications.Tools.PlatformMocks;
using Altinn.Notifications.Tools.PlatformMocks.Configuration;
using Altinn.Notifications.Tools.PlatformMocks.Mocks;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.PlatformMocks;

/// <summary>
/// Calls the mocks through the notifications API's own clients, so the tests break if the mocks drift from what the API expects.
/// </summary>
public class PlatformMocksServiceTests
{
    private const string Person = "12345678901";
    private const string Organization = "310000002";

    [Fact]
    public async Task Profile_GetUserContactPoints_GivesEveryPersonContactPointsExceptTheListedOnes()
    {
        await using var mocks = await StartAsync();
        var profile = mocks.Get<IProfileClient>();

        var contactPoints = await profile.GetUserContactPoints([Person, "99999999901", "88888888801"], false);

        var person = Assert.Single(contactPoints, c => c.NationalIdentityNumber == Person);
        Assert.Equal("12345678901@example.com", person.Email);
        Assert.Equal("+4799999999", person.MobileNumber);
        Assert.False(person.IsReserved);
        Assert.Equal(ProfileMock.UserId(Person), person.UserId);

        Assert.True(Assert.Single(contactPoints, c => c.NationalIdentityNumber == "99999999901").IsReserved);

        var withoutContactPoints = Assert.Single(contactPoints, c => c.NationalIdentityNumber == "88888888801");
        Assert.Empty(withoutContactPoints.Email);
        Assert.Empty(withoutContactPoints.MobileNumber);
    }

    [Fact]
    public async Task Profile_GetOrganizationContactPoints_GivesEveryOrganizationNotificationAddressesExceptTheListedOnes()
    {
        await using var mocks = await StartAsync();
        var profile = mocks.Get<IProfileClient>();

        var contactPoints = await profile.GetOrganizationContactPoints([Organization, "999999999"]);

        var organization = Assert.Single(contactPoints, c => c.OrganizationNumber == Organization);
        Assert.Equal(["310000002@example.com"], organization.EmailList);
        Assert.Equal(["+4799999999"], organization.MobileNumberList);
        Assert.Equal(ProfileMock.PartyId(Organization), organization.PartyId);

        var withoutContactPoints = Assert.Single(contactPoints, c => c.OrganizationNumber == "999999999");
        Assert.Empty(withoutContactPoints.EmailList);
        Assert.Empty(withoutContactPoints.MobileNumberList);
    }

    [Fact]
    public async Task Profile_GetExternalIdentityContactPoints_TakesTheEmailFromIdportenEmailIdentities()
    {
        await using var mocks = await StartAsync();
        var profile = mocks.Get<IProfileClient>();

        var contactPoints = await profile.GetExternalIdentityContactPoints(
            ["urn:altinn:person:idporten-email:ola@example.org", "urn:altinn:person:legacy-selfidentified:ola"]);

        var email = Assert.Single(contactPoints, c => c.ExternalIdentity.Contains("idporten-email"));
        Assert.Equal("ola@example.org", email.Email);
        Assert.Empty(email.MobileNumber);

        var legacy = Assert.Single(contactPoints, c => c.ExternalIdentity.Contains("legacy-selfidentified"));
        Assert.Equal("ola@example.com", legacy.Email);
        Assert.Equal("+4799999999", legacy.MobileNumber);
    }

    [Fact]
    public async Task ProfileAndAuthorization_RegisteredUsers_AreKeptByTheApisAuthorizationFilter()
    {
        await using var mocks = await StartAsync();

        var registered = await mocks.Get<IProfileClient>().GetUserRegisteredContactPoints([Organization], "urn:altinn:resource:ttd-test");
        var authorized = await mocks.Get<IAuthorizationService>().AuthorizeUserContactPointsForResource(registered, "urn:altinn:resource:ttd-test");

        var organization = Assert.Single(authorized);
        var user = Assert.Single(organization.UserContactPoints);
        Assert.Equal(ProfileMock.RegisteredUser(Organization), user.NationalIdentityNumber);
        Assert.Equal("31000000201@example.com", user.Email);
    }

    [Fact]
    public async Task Authorization_DeniedUser_IsFilteredOutByTheApi()
    {
        int deniedUser = ProfileMock.UserId(ProfileMock.RegisteredUser(Organization));
        await using var mocks = await StartAsync(s => s.Authorization.DeniedUsers = [deniedUser]);

        var registered = await mocks.Get<IProfileClient>().GetUserRegisteredContactPoints([Organization], "app_ttd_test-app");
        var authorized = await mocks.Get<IAuthorizationService>().AuthorizeUserContactPointsForResource(registered, "app_ttd_test-app");

        Assert.Empty(Assert.Single(authorized).UserContactPoints);
    }

    [Fact]
    public async Task Register_GetPartyDetails_NamesEveryParty()
    {
        await using var mocks = await StartAsync();

        var parties = await mocks.Get<IRegisterClient>().GetPartyDetails([Organization], [Person]);

        Assert.Equal("Testorganisasjon 310000002 AS", Assert.Single(parties, p => p.OrganizationNumber == Organization).Name);
        Assert.Equal("Testperson 12345678901", Assert.Single(parties, p => p.NationalIdentityNumber == Person).Name);
    }

    [Theory]
    [InlineData("/condition/true", true)]
    [InlineData("/condition/false", false)]
    public async Task Conditions_FixedAnswers_AreReadByTheApi(string path, bool expected)
    {
        await using var mocks = await StartAsync();

        var result = await ConditionClient().CheckSendCondition(new Uri(mocks.Urls["conditions"] + path));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Match(sendNotification => sendNotification, _ => !expected));
    }

    [Theory]
    [InlineData("/condition/corrupt", null)]
    [InlineData("/condition/error", 500)]
    public async Task Conditions_BrokenAnswers_AreErrorsInTheApi(string path, int? expectedStatusCode)
    {
        await using var mocks = await StartAsync();

        var result = await ConditionClient().CheckSendCondition(new Uri(mocks.Urls["conditions"] + path));

        Assert.True(result.IsError);
        Assert.Equal(expectedStatusCode, result.Match(_ => -1, error => error.StatusCode));
    }

    [Fact]
    public async Task Disabled_System_IsNotStarted()
    {
        await using var mocks = await StartAsync(s => s.Register.Enabled = false);

        Assert.DoesNotContain("register", mocks.Urls.Keys);
        Assert.Contains("profile", mocks.Urls.Keys);
    }

    [Fact]
    public async Task UnknownPath_WithoutFallback_IsNotFound()
    {
        await using var mocks = await StartAsync();
        using var http = new HttpClient();

        using var response = await http.GetAsync(mocks.Urls["profile"] + "/profile/api/v1/users/12345", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPath_WithFallback_IsForwardedToFallbackUnderTheSystemName()
    {
        using var simulator = WireMockServer.Start();
        simulator
            .Given(Request.Create().WithPath("/intercept/profile/profile/api/v1/users/12345").UsingPost().WithHeader("X-Test", "yes"))
            .RespondWith(Response.Create().WithStatusCode(418).WithHeader("X-Simulator", "answered").WithBody("from the operator"));

        await using var mocks = await StartAsync(s => s.FallbackUrl = simulator.Url + "/intercept/");
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, mocks.Urls["profile"] + "/profile/api/v1/users/12345?full=true")
        {
            Content = new StringContent("""{"id":1}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Test", "yes");

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode)418, response.StatusCode);
        Assert.Equal("answered", response.Headers.GetValues("X-Simulator").Single());
        Assert.Equal("from the operator", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var forwarded = Assert.Single(simulator.LogEntries).RequestMessage!;
        Assert.Equal("?full=true", forwarded.RawQuery);
        Assert.Equal("""{"id":1}""", forwarded.Body);
    }

    [Fact]
    public async Task KnownPath_WithFallback_IsStillAnsweredByTheMock()
    {
        using var simulator = WireMockServer.Start();
        await using var mocks = await StartAsync(s => s.FallbackUrl = simulator.Url + "/intercept/");

        var parties = await mocks.Get<IRegisterClient>().GetPartyDetails([Organization], null);

        Assert.Single(parties);
        Assert.Empty(simulator.LogEntries);
    }

    [Fact]
    public async Task Maskinporten_TokenRequest_GetsATokenWithTheScopeFromTheGrant()
    {
        await using var mocks = await StartAsync();
        using var http = new HttpClient();

        // The shape Altinn.ApiClients.Maskinporten sends: a JWT bearer grant with the scope in the signed assertion.
        string claims = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"scope":"altinn:system/notifications.condition.check","iss":"client"}""")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = $"eyJhbGciOiJSUzI1NiJ9.{claims}.signature",
        });

        using var response = await http.PostAsync(mocks.Urls["maskinporten"] + "/token", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith("mock-maskinporten-token-", token.RootElement.GetProperty("access_token").GetString());
        Assert.Equal("Bearer", token.RootElement.GetProperty("token_type").GetString());
        Assert.Equal(MaskinportenMock.ExpiresInSeconds, token.RootElement.GetProperty("expires_in").GetInt32());
        Assert.Equal("altinn:system/notifications.condition.check", token.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Maskinporten_GrantThatIsNotAJwt_StillGetsAToken()
    {
        await using var mocks = await StartAsync();
        using var http = new HttpClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["assertion"] = "not-a-jwt" });

        using var response = await http.PostAsync(mocks.Urls["maskinporten"] + "/token", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, token.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Maskinporten_ConditionClientWithAppHostSettings_GetsItsTokenFromTheMock()
    {
        await using var mocks = await StartAsync();
        string maskinportenUrl = mocks.Urls["maskinporten"];

        // The API's appsettings.Development.json, with the settings the Aspire app host adds on top.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SendConditionClient:MaskinportenSettings:Environment"] = "test",
                ["SendConditionClient:MaskinportenSettings:Scope"] = "altinn:system/notifications.condition.check",
                ["SendConditionClient:MaskinportenSettings:TokenEndpoint"] = maskinportenUrl + "/token",
                ["SendConditionClient:MaskinportenSettings:AssertionAudience"] = maskinportenUrl + "/",
                ["SendConditionClient:MaskinportenSettings:ClientId"] = "altinn-notifications-local",
                ["SendConditionClient:MaskinportenSettings:EncodedJwk"] = CreateSigningKey(),
            })
            .Build();
        var settings = new MaskinportenSettings();
        config.GetSection("SendConditionClient:MaskinportenSettings").Bind(settings);

        // The handler chain AddMaskinportenHttpClient puts in front of the API's condition client. It is built by hand because
        // the library keeps the settings of the first registration per client type in a static list, for the whole test run.
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var maskinporten = new MaskinportenService(new HttpClient(), NullLogger<MaskinportenService>.Instance, new MemoryTokenCacheProvider(cache));
        var conditionRequests = new RecordingHandler(new HttpClientHandler());
        var tokenHandler = new MaskinportenTokenHandler(maskinporten, new SettingsJwkClientDefinition { ClientSettings = settings })
        {
            InnerHandler = conditionRequests,
        };

        var result = await new SendConditionClient(new HttpClient(tokenHandler)).CheckSendCondition(new Uri(mocks.Urls["conditions"] + "/condition/true"));

        Assert.True(result.IsSuccess);
        var authorization = Assert.Single(conditionRequests.Authorizations);
        Assert.Equal("Bearer", authorization?.Scheme);
        Assert.StartsWith("mock-maskinporten-token-", authorization?.Parameter);
    }

    // The API's condition client without the Maskinporten handler in front. The token endpoint is tested on its own above.
    private static SendConditionClient ConditionClient() => new(new HttpClient());

    // A throwaway RSA key as a base64-encoded JWK, as the Aspire app host makes one.
    private static string CreateSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var key = rsa.ExportParameters(includePrivateParameters: true);
        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["kid"] = "altinn-notifications-local",
            ["e"] = Base64Url(key.Exponent),
            ["n"] = Base64Url(key.Modulus),
            ["d"] = Base64Url(key.D),
            ["p"] = Base64Url(key.P),
            ["q"] = Base64Url(key.Q),
            ["dp"] = Base64Url(key.DP),
            ["dq"] = Base64Url(key.DQ),
            ["qi"] = Base64Url(key.InverseQ),
        };

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(jwk));

        static string Base64Url(byte[]? bytes) => Convert.ToBase64String(bytes!).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static async Task<RunningMocks> StartAsync(Action<PlatformMocksSettings>? configure = null)
    {
        var settings = new PlatformMocksSettings();
        settings.Profile.Port = FreePort();
        settings.Register.Port = FreePort();
        settings.Authorization.Port = FreePort();
        settings.Conditions.Port = FreePort();
        settings.Maskinporten.Port = FreePort();
        settings.Profile.ReservedPersons = ["99999999901"];
        settings.Profile.PersonsWithoutContactPoints = ["88888888801"];
        settings.Profile.OrganizationsWithoutContactPoints = ["999999999"];
        configure?.Invoke(settings);

        var service = new PlatformMocksService(Options.Create(settings), NullLogger<PlatformMocksService>.Instance);
        await service.StartAsync(TestContext.Current.CancellationToken);
        return new RunningMocks(service);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    // Records the Authorization header of each request that gets past the Maskinporten handler.
    private sealed class RecordingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<System.Net.Http.Headers.AuthenticationHeaderValue?> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class RunningMocks : IAsyncDisposable
    {
        private readonly PlatformMocksService _service;
        private readonly ServiceProvider _clients;

        public RunningMocks(PlatformMocksService service)
        {
            _service = service;
            Urls = service.Urls;

            // The same settings the Aspire app host gives the API.
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PlatformSettings:ApiProfileEndpoint"] = Url("profile") + "/profile/api/v1/",
                    ["PlatformSettings:ApiRegisterEndpoint"] = Url("register") + "/register/api/v1/",
                    ["PlatformSettings:ApiAuthorizationEndpoint"] = Url("authorization") + "/authorization/api/v1/",
                    ["NotificationConfig:AuthorizationBatchSize"] = "500",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<NotificationConfig>(config.GetSection(nameof(NotificationConfig)));
            services.AddAltinnClients(config);
            services.AddAuthorizationService(config);
            _clients = services.BuildServiceProvider();
        }

        public IReadOnlyDictionary<string, string> Urls { get; }

        public T Get<T>()
            where T : notnull => _clients.GetRequiredService<T>();

        public async ValueTask DisposeAsync()
        {
            await _clients.DisposeAsync();
            await _service.StopAsync(TestContext.Current.CancellationToken);
        }

        private string Url(string system) => Urls.TryGetValue(system, out var url) ? url : "http://localhost:1";
    }
}
