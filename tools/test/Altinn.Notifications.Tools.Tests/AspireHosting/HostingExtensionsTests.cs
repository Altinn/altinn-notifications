using System.Text.RegularExpressions;

using Altinn.Notifications.Tools.TestHarness.Triggers;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AspireHosting;

/// <summary>
/// The app host extensions in <c>Altinn.Notifications.Aspire.Hosting</c>, checked on the app model without starting anything.
/// </summary>
public class HostingExtensionsTests
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

        var environment = await EnvironmentOf(email);
        Assert.Equal($"endpoint=http://localhost:5095/;accesskey={AcsMockBuilderExtensions.AccessKey}", environment["CommunicationServicesSettings__ConnectionString"]);
        Assert.Equal(AcsMockBuilderExtensions.AccessKey, (await EnvironmentOf(acs))["AcsMockSettings__AccessKey"]);
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
        Assert.Equal("6121", mockEnvironment["PlatformMocksSettings__Register__Port"]);
        Assert.Equal("http://localhost:5197/token", apiEnvironment["SendConditionClient__MaskinportenSettings__TokenEndpoint"]);
        Assert.NotEmpty(apiEnvironment["SendConditionClient__MaskinportenSettings__EncodedJwk"]);
        Assert.Contains(api.Resource.Annotations.OfType<WaitAnnotation>(), w => w.Resource == mocks.Resource);
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
    public void Mocks_AreStartedFromTheirProjectsUnderToolsSrc()
    {
        var harness = _builder.AddTestHarness("controlpanel", 5096, intercept: false, timeoutSeconds: 120);
        IResourceBuilder<ProjectResource>[] mocks =
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
    public void AspireHostingPackage_IsTheVersionOfTheAppHostSdk()
    {
        string root = RepositoryRoot();
        string appHost = File.ReadAllText(Path.Combine(root, "tools", "aspire", "apphost.cs"));
        string packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        string sdkVersion = Regex.Match(appHost, @"#:sdk Aspire\.AppHost\.Sdk@(\S+)").Groups[1].Value;
        string packageVersion = Regex.Match(packages, @"Include=""Aspire\.Hosting"" Version=""([^""]+)""").Groups[1].Value;

        Assert.NotEmpty(sdkVersion);
        Assert.Equal(sdkVersion, packageVersion);
    }

    private IResourceBuilder<ProjectResource> AddService(string name, int port) =>
        _builder.AddProject(name, Path.Combine(RepositoryRoot(), "components", name, "service.csproj"), launchProfileName: null)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false);

    /// <summary>
    /// The resource's environment, after giving every endpoint the address the orchestrator would, so references to them resolve.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> EnvironmentOf(IResourceBuilder<ProjectResource> resource)
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
}
