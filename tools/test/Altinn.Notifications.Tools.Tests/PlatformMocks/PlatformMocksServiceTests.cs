using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Tools.PlatformMocks;
using Altinn.Notifications.Tools.PlatformMocks.Configuration;
using Altinn.Notifications.Tools.PlatformMocks.Mocks;

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

        var result = await mocks.Get<IConditionClient>().CheckSendCondition(new Uri(mocks.Urls["conditions"] + path));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Match(sendNotification => sendNotification, _ => !expected));
    }

    [Theory]
    [InlineData("/condition/corrupt", null)]
    [InlineData("/condition/error", 500)]
    public async Task Conditions_BrokenAnswers_AreErrorsInTheApi(string path, int? expectedStatusCode)
    {
        await using var mocks = await StartAsync();

        var result = await mocks.Get<IConditionClient>().CheckSendCondition(new Uri(mocks.Urls["conditions"] + path));

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

    private static async Task<RunningMocks> StartAsync(Action<PlatformMocksSettings>? configure = null)
    {
        var settings = new PlatformMocksSettings();
        settings.Profile.Port = FreePort();
        settings.Register.Port = FreePort();
        settings.Authorization.Port = FreePort();
        settings.Conditions.Port = FreePort();
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
                    ["SendConditionClient:UseMaskinporten"] = "false",
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
