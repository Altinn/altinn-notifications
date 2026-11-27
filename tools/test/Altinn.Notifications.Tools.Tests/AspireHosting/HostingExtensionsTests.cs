using System.Text.RegularExpressions;

using Altinn.Notifications.Tools.TestHarness.Triggers;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AspireHosting;

/// <summary>
/// The app host extensions in <c>Altinn.Notifications.Aspire.Hosting</c>, checked on the app model without starting anything.
/// </summary>
public partial class HostingExtensionsTests
{
    private readonly IDistributedApplicationBuilder _builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], DisableDashboard = true });

    [Fact]
    public async Task AcsMock_WithIntercept_HandsUnknownRequestsToTheControlPanel_AndWaitsForIt()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: true, timeoutSeconds: 120);

        var acs = _builder.AddAcsMock("mock-acs", 5095, harness);

        var environment = await EnvironmentOf(acs);
        Assert.Equal("http://localhost:5096/intercept/", environment["AcsMockSettings__FallbackUrl"]);
        Assert.Equal("http://localhost:5096/mirror/", environment["AcsMockSettings__MirrorUrl"]);
        Assert.Contains(acs.Resource.Annotations.OfType<WaitAnnotation>(), w => w.Resource == harness.Resource);
        Assert.Contains(acs.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(), r => r.Resource == harness.Resource && r.Type == "Parent");
    }

    [Fact]
    public async Task AcsMock_WithoutIntercept_AnswersItself_AndDoesNotWait()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);

        var acs = _builder.AddAcsMock("mock-acs", 5095, harness);

        var environment = await EnvironmentOf(acs);
        Assert.Equal(string.Empty, environment["AcsMockSettings__FallbackUrl"]);
        Assert.Equal("http://localhost:5096/mirror/", environment["AcsMockSettings__MirrorUrl"]);
        Assert.DoesNotContain(acs.Resource.Annotations.OfType<WaitAnnotation>(), w => w.Resource == harness.Resource);
    }

    [Fact]
    public async Task WithAcsMock_SignsWithTheMocksKey_AndWaitsForIt()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        var acs = _builder.AddAcsMock("mock-acs", 5095, harness);
        var email = AddService("email", 5091);

        email.WithAcsMock(acs);

        // A key generated for the checkout, valid base64 for the SDK, in both the mock and the connection string.
        string accessKey = (await EnvironmentOf(acs))["AcsMockSettings__AccessKey"];
        Assert.Equal(44, accessKey.Length);
        Assert.NotEmpty(Convert.FromBase64String(accessKey));
        Assert.True(Secret("mock-acs-access-key").Secret);

        var environment = await EnvironmentOf(email);
        Assert.Equal($"endpoint=http://localhost:5095/;accesskey={accessKey}", environment["CommunicationServicesSettings__ConnectionString"]);
        Assert.Contains(email.Resource.Annotations.OfType<WaitAnnotation>(), w => w.Resource == acs.Resource);
    }

    [Fact]
    public async Task WithMockAuth_PointsTheApiToTheIssuer()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        var auth = _builder.AddMockAuth("mock-auth", 5094, harness);
        var api = AddService("api", 5090);

        api.WithMockAuth(auth);

        var environment = await EnvironmentOf(api);
        Assert.Equal("http://localhost:5094/authentication/api/v1/openid/", environment["GeneralSettings__OpenIdWellKnownEndpoint"]);

        // A signing key for this run, which the issuer can import.
        var authEnvironment = await EnvironmentOf(auth);
        using var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(authEnvironment["MockAuthSettings__SigningKey"]), out _);
        Assert.Equal(2048, rsa.KeySize);

        // Basic auth with a password generated for the checkout, which Bruno, k6 and the dashboard commands get from GetMockAuthCredentials.
        var credentials = auth.GetMockAuthCredentials();
        Assert.Equal(credentials.Username, authEnvironment["MockAuthSettings__Username"]);
        Assert.Equal(await credentials.Password.GetValueAsync(TestContext.Current.CancellationToken), authEnvironment["MockAuthSettings__Password"]);
        Assert.Equal(32, authEnvironment["MockAuthSettings__Password"].Length);
        Assert.True(Secret("mock-auth-password").Secret);
    }

    [Fact]
    public void WithAcsMock_WithoutTheAcsMock_Throws()
    {
        var email = AddService("email", 5091);
        var other = AddService("other", 5098);

        var error = Assert.Throws<ArgumentException>(() => email.WithAcsMock(other));
        Assert.Contains("AddAcsMock", error.Message);
    }

    [Fact]
    public void GetMockAuthCredentials_WithoutTheIssuer_Throws()
    {
        var other = AddService("other", 5098);

        var error = Assert.Throws<ArgumentException>(() => other.GetMockAuthCredentials());
        Assert.Contains("AddMockAuth", error.Message);
    }

    [Fact]
    public void LinkMobilityMock_Interactive_NeedsIntercept()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);

        var error = Assert.Throws<InvalidOperationException>(() => _builder.AddLinkMobilityMock("wiremock-linkmobility", 5093, harness, interactive: true));
        Assert.Contains("TestHarness:Intercept", error.Message);
    }

    [Fact]
    public async Task WithLinkMobilityMock_Interactive_OutwaitsTheControlPanel_AndGetsReportsBack()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: true, timeoutSeconds: 120);
        var linkMobility = _builder.AddLinkMobilityMock("wiremock-linkmobility", 5093, harness, interactive: true);
        var sms = AddService("sms", 5092);

        sms.WithLinkMobilityMock(linkMobility);

        var smsEnvironment = await EnvironmentOf(sms);
        var mockEnvironment = await EnvironmentOf(linkMobility);
        Assert.Equal("http://localhost:5093", smsEnvironment["SmsGatewaySettings__Endpoint"]);
        Assert.Equal("130", smsEnvironment["SmsGatewaySettings__TimeoutInSeconds"]);
        Assert.Equal(mockEnvironment["LinkMobilityProxySettings__DeliveryReportUsername"], smsEnvironment["SmsDeliveryReportSettings__UserSettings__Username"]);
        Assert.Equal(mockEnvironment["LinkMobilityProxySettings__DeliveryReportPassword"], smsEnvironment["SmsDeliveryReportSettings__UserSettings__Password"]);
        Assert.Equal(32, mockEnvironment["LinkMobilityProxySettings__DeliveryReportPassword"].Length);
        Assert.True(Secret("wiremock-linkmobility-delivery-report-password").Secret);
        Assert.Equal("http://localhost:5092/notifications/sms/api/v1/reports", mockEnvironment["LinkMobilityProxySettings__DeliveryReportUrl"]);
    }

    [Fact]
    public async Task WithLinkMobilityMock_NotInteractive_KeepsTheSmsServicesTimeout()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: true, timeoutSeconds: 120);
        var linkMobility = _builder.AddLinkMobilityMock("wiremock-linkmobility", 5093, harness);
        var sms = AddService("sms", 5092);

        sms.WithLinkMobilityMock(linkMobility);

        Assert.False((await EnvironmentOf(sms)).ContainsKey("SmsGatewaySettings__TimeoutInSeconds"));
    }

    [Fact]
    public async Task WithPlatformMocks_LeavesSystemsThatAreOffToTheApisOwnSettings()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Profile:Enabled"] = "false", ["Register:Port"] = "6121" })
            .Build();
        var mocks = _builder.AddPlatformMocks("wiremock-platform", harness, settings);
        var api = AddService("api", 5090);

        api.WithPlatformMocks(mocks);

        var apiEnvironment = await EnvironmentOf(api);
        var mockEnvironment = await EnvironmentOf(mocks);
        Assert.False(apiEnvironment.ContainsKey("PlatformSettings__ApiProfileEndpoint"));
        Assert.Equal("False", mockEnvironment["PlatformMocksSettings__Profile__Enabled"]);
        Assert.Equal("http://localhost:6121/register/api/v1/", apiEnvironment["PlatformSettings__ApiRegisterEndpoint"]);
        Assert.Equal("http://localhost:6121/register/api/", apiEnvironment["RegisterSettings__ApiEndpoint"]);
        Assert.Equal("6121", mockEnvironment["PlatformMocksSettings__Register__Port"]);
        Assert.Equal("http://localhost:5197/token", apiEnvironment["SendConditionClient__MaskinportenSettings__TokenEndpoint"]);
        Assert.NotEmpty(apiEnvironment["SendConditionClient__MaskinportenSettings__EncodedJwk"]);
        Assert.Contains(api.Resource.Annotations.OfType<WaitAnnotation>(), w => w.Resource == mocks.Resource);
    }

    [Fact]
    public async Task WithPlatformMocks_RegisterMaskinportenAndConditionsOff_LeavesTheirSettingsToTheApi()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Register:Enabled"] = "false", ["Maskinporten:Enabled"] = "false", ["Conditions:Enabled"] = "false" })
            .Build();
        var mocks = _builder.AddPlatformMocks("wiremock-platform", harness, settings);
        var api = AddService("api", 5090);

        api.WithPlatformMocks(mocks);

        var apiEnvironment = await EnvironmentOf(api);
        Assert.Equal("http://localhost:5131/profile/api/v1/", apiEnvironment["PlatformSettings__ApiProfileEndpoint"]);
        Assert.False(apiEnvironment.ContainsKey("PlatformSettings__ApiRegisterEndpoint"));
        Assert.False(apiEnvironment.ContainsKey("RegisterSettings__ApiEndpoint"));
        Assert.DoesNotContain(apiEnvironment.Keys, key => key.StartsWith("SendConditionClient__MaskinportenSettings__"));
    }

    [Fact]
    public void WithPlatformMocks_OtherResource_IsRejected()
    {
        var api = AddService("api", 5090);

        var error = Assert.Throws<ArgumentException>(() => api.WithPlatformMocks(AddService("profile", 5030)));
        Assert.Contains("AddPlatformMocks", error.Message);
    }

    [Fact]
    public void WithLinkMobilityMock_OtherResource_IsRejected()
    {
        var sms = AddService("sms", 5092);

        var error = Assert.Throws<ArgumentException>(() => sms.WithLinkMobilityMock(AddService("gateway", 5093)));
        Assert.Contains("AddLinkMobilityMock", error.Message);
    }

    [Fact]
    public async Task WithTriggers_PassesJobOverrides_AndAddsTheTriggerCommands()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        var api = AddService("api", 5090);
        var jobs = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jobs:sendsms:Hours"] = "7-16" })
            .Build()
            .GetSection("Jobs");

        harness.WithTriggers(api, enabled: false, jobs);

        var environment = await EnvironmentOf(harness);
        Assert.Equal("False", environment["TriggerSettings__Enabled"]);
        Assert.Equal("http://localhost:5090", environment["TriggerSettings__ApiBaseUrl"]);
        Assert.Equal("7-16", environment["TriggerSettings__Jobs__sendsms__Hours"]);
        Assert.Contains(harness.Resource.Annotations.OfType<WaitAnnotation>(), w => w.Resource == api.Resource);

        var commands = harness.Resource.Annotations.OfType<ResourceCommandAnnotation>().Select(c => c.Name).ToList();
        Assert.Subset(new HashSet<string>(commands), new HashSet<string> { "run-trigger", "pause-triggers", "resume-triggers", "clear-phone", "clear-inbox" });
    }

    [Fact]
    public void RunTrigger_OffersEveryJobInTheControlPanelsCatalog()
    {
        Assert.Equal(TriggerCatalog.All.Select(d => d.Name), TestHarnessBuilderExtensions.TriggerJobs);
    }

    [Fact]
    public void WithTestMessageCommands_AddsSendCommands_WithTheRecipientAsArgument()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        var auth = _builder.AddMockAuth("mock-auth", 5094, harness);

        harness.WithTestMessageCommands(AddService("api", 5090), auth);

        var commands = harness.Resource.Annotations.OfType<ResourceCommandAnnotation>().ToDictionary(c => c.Name);
        Assert.Equal(["recipient", "text"], commands["send-test-sms"].Arguments.Select(a => a.Name));
        Assert.Equal(["recipient", "subject", "body"], commands["send-test-email"].Arguments.Select(a => a.Name));
    }

    [Fact]
    public void WithQueueStatusCommand_OffersTheEmulatorsQueues_AndEveryStatus()
    {
        var serviceBus = _builder.AddExecutable("servicebus", "docker", ".", "logs", "-f", "servicebus-emulator");

        serviceBus.WithQueueStatusCommand(Path.Combine(RepositoryRoot(), "tools", "asb-emulator", "config.json"), 5300);

        var command = Assert.Single(serviceBus.Resource.Annotations.OfType<ResourceCommandAnnotation>(), c => c.Name == "set-queue-status");
        var queue = command.Arguments.Single(a => a.Name == "queue");
        var status = command.Arguments.Single(a => a.Name == "status");
        Assert.Contains(queue.Options!, o => o.Key == "altinn.notifications.sms.send");
        Assert.Equal(["Active", "ReceiveDisabled", "SendDisabled", "Disabled"], status.Options!.Select(o => o.Key));
        Assert.Equal("ReceiveDisabled", status.Value);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, ResourceCommandState.Enabled)]
    [InlineData(HealthStatus.Unhealthy, ResourceCommandState.Disabled)]
    [InlineData(null, ResourceCommandState.Disabled)]
    public void SetQueueStatus_IsEnabledOnlyWhileTheEmulatorIsHealthy(HealthStatus? health, ResourceCommandState expected)
    {
        Assert.Equal(expected, ServiceBusEmulatorBuilderExtensions.IsEnabled(health));
    }

    [Fact]
    public void Mocks_AreStartedFromTheirProjectsUnderToolsSrc()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        IResourceBuilder<DotnetProjectResource>[] mocks =
        [
            harness,
            _builder.AddMockAuth("mock-auth", 5094, harness),
            _builder.AddAcsMock("mock-acs", 5095, harness),
            _builder.AddLinkMobilityMock("wiremock-linkmobility", 5093, harness),
            _builder.AddPlatformMocks("wiremock-platform", harness, new ConfigurationBuilder().Build()),
        ];

        foreach (var mock in mocks)
        {
            string path = mock.Resource.GetProjectMetadata().ProjectPath;
            Assert.True(File.Exists(path), $"{mock.Resource.Name}: {path} does not exist.");
        }
    }

    [Fact]
    public void AspirePackages_HaveOneVersion_AndTheAppHostTakesIt()
    {
        string root = RepositoryRoot();
        string appHost = File.ReadAllText(Path.Combine(root, "tools", "aspire", "apphost.cs"));
        string packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        var versions = AspirePackageVersion().Matches(packages).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        Assert.Contains("Aspire.Hosting", versions.Keys);
        Assert.Contains("Aspire.Hosting.AppHost", versions.Keys);
        Assert.Contains("Aspire.Hosting.Dotnet", versions.Keys);
        Assert.Single(versions.Values.Distinct());

        // A version on a #:package directive would override the central one.
        Assert.DoesNotContain("#:package Aspire.Hosting.AppHost@", appHost);
        Assert.DoesNotContain("#:package Aspire.Hosting.Dotnet@", appHost);
    }

    private ParameterResource Secret(string name) => _builder.Resources.OfType<ParameterResource>().Single(p => p.Name == name);

    private IResourceBuilder<DotnetProjectResource> AddService(string name, int port) =>
        _builder.AddDotnetProject(name, Path.Combine(RepositoryRoot(), "components", name, "service.csproj"), options => options.ExcludeLaunchProfile = true)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false);

    /// <summary>
    /// The resource's environment, after giving every endpoint the address the orchestrator would, so references to them resolve.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> EnvironmentOf(IResourceBuilder<DotnetProjectResource> resource)
    {
        foreach (var endpoint in _builder.Resources.SelectMany(r => r.Annotations.OfType<EndpointAnnotation>()).Where(e => e.AllocatedEndpoint is null))
        {
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", endpoint.Port!.Value);
        }

        var configuration = await ExecutionConfigurationBuilder.Create(resource.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), NullLogger.Instance, CancellationToken.None);
        return configuration.EnvironmentVariables.ToDictionary();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Directory.Packages.props not found above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"Include=""(Aspire\.[^""]+)"" Version=""([^""]+)""")]
    private static partial Regex AspirePackageVersion();
}
