#:sdk Aspire.AppHost.Sdk@13.6.1
#:property AspireUseCliBundle=true

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string Components = "../../components";

var builder = DistributedApplication.CreateBuilder(args);

string[] projects =
[
    $"{Components}/api/src/Altinn.Notifications/Altinn.Notifications.csproj",
    $"{Components}/email-service/src/Altinn.Notifications.Email/Altinn.Notifications.Email.csproj",
    $"{Components}/sms-service/src/Altinn.Notifications.Sms/Altinn.Notifications.Sms.csproj",
];

// Parallel dotnet run builds of the shared project collide, so build one by one first.
foreach (var project in projects)
{
    using var build = Process.Start("dotnet", ["build", Path.Combine(builder.AppHostDirectory, project)]);
    build.WaitForExit();
    if (build.ExitCode != 0)
    {
        throw new InvalidOperationException($"Build failed for {project}");
    }
}

// The dashboard lists resources in the order they are added.
var api = AddService("api", projects[0], 5090, "swagger", "altinn-notifications");
var email = AddService("email", projects[1], 5091, "swagger", "altinn-notifications-email");
var sms = AddService("sms", projects[2], 5092, string.Empty, "altinn-notifications-sms");

// The containers are started outside Aspire so they keep running when it stops; the resources only tail their logs.
builder.Configuration
    .AddJsonFile(Path.Combine(builder.AppHostDirectory, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(builder.AppHostDirectory, "appsettings.Local.json"), optional: true);

// podman or docker. Both take the same start, logs and compose commands.
var containerRuntime = builder.Configuration["ContainerRuntime"] ?? "podman";

if (builder.Configuration.GetValue("Postgres:UseContainer", true))
{
    var postgresContainer = builder.Configuration["Postgres:ContainerName"] ?? "notifications-db";
    StartContainer(".", ["start", postgresContainer]);
    var postgres = builder.AddExecutable("postgres", containerRuntime, ".", "logs", "-f", postgresContainer);
    api.WaitFor(postgres);
}

// The API's connection strings point to 5432. Another port lets the app host use its own database next to an existing one.
var postgresPort = builder.Configuration.GetValue("Postgres:Port", 5432);
if (postgresPort != 5432)
{
    string connectionString = "Host=localhost;Port=" + postgresPort + ";Username={0};Password={{0}};Database=notificationsdb";
    api.WithEnvironment("PostgreSQLSettings__AdminConnectionString", string.Format(connectionString, "platform_notifications_admin"))
        .WithEnvironment("PostgreSQLSettings__ConnectionString", string.Format(connectionString, "platform_notifications"));
}

// mock: the API trusts tokens from a local issuer, so no at23 token is needed. at23: the API validates against at23 as configured in its appsettings.
var authMode = builder.Configuration["Auth:Mode"] ?? "mock";
if (authMode == "mock")
{
    var authPort = builder.Configuration.GetValue("Auth:Port", 5094);
    string authUrl = "http://localhost:" + authPort;
    var auth = builder.AddProject("auth", "../src/Altinn.Notifications.Tools.MockAuth/Altinn.Notifications.Tools.MockAuth.csproj", launchProfileName: null)
        .WithEnvironment("MockAuthSettings__Port", authPort.ToString())
        .WithUrl(authUrl + "/authentication/api/v1/openid/.well-known/openid-configuration", "OpenID configuration");

    api.WithEnvironment("GeneralSettings__OpenIdWellKnownEndpoint", authUrl + "/authentication/api/v1/openid/")
        .WaitFor(auth);
}
else if (authMode != "at23")
{
    throw new InvalidOperationException($"Auth:Mode must be 'mock' or 'at23', not '{authMode}'.");
}

// The services only use AMQP on 5672. The emulator's HTTP port is just for the health check, so it can move if 5300 is taken.
var serviceBusHealthPort = builder.Configuration.GetValue("ServiceBus:HealthPort", 5300);
StartContainer("../asb-emulator", ["compose", "up", "-d"], new() { ["EMULATOR_HOST_HTTP_PORT"] = serviceBusHealthPort.ToString() });
var serviceBus = builder.AddExecutable("servicebus", containerRuntime, ".", "logs", "-f", "servicebus-emulator")
    .WithHttpEndpoint(port: serviceBusHealthPort, targetPort: serviceBusHealthPort, name: "management", isProxied: false)
    .WithHttpHealthCheck("/health", endpointName: "management");

api.WaitFor(serviceBus);
email.WaitFor(serviceBus);
sms.WaitFor(serviceBus);

// The simulator does two things. With Simulator:Enabled the mocks hand requests they do not recognise to it, where an operator
// answers them. With Triggers:Enabled it calls the API's trigger endpoints on an interval, like the cron jobs in the cluster, so
// orders are sent without anyone calling /trigger/send* by hand. It starts when either is on.
bool interceptEnabled = builder.Configuration.GetValue("Simulator:Enabled", false);
bool triggersEnabled = builder.Configuration.GetValue("Triggers:Enabled", true);
string simulatorFallbackUrl = string.Empty;
IResourceBuilder<ProjectResource>? simulator = null;
if (interceptEnabled || triggersEnabled)
{
    var simulatorPort = builder.Configuration.GetValue("Simulator:Port", 5096);
    string simulatorUrl = "http://localhost:" + simulatorPort;

    // The dashboard's own URL, so each held request can link to its trace.
    string dashboardUrl = (builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty).Split(';')[0];
    var simulatorResource = builder.AddProject("simulator", "../src/Altinn.Notifications.Tools.Simulator/Altinn.Notifications.Tools.Simulator.csproj", launchProfileName: null)
        .WithEnvironment("SimulatorSettings__Port", simulatorPort.ToString())
        .WithEnvironment("SimulatorSettings__Intercept", interceptEnabled.ToString())
        .WithEnvironment("SimulatorSettings__TimeoutSeconds", builder.Configuration.GetValue("Simulator:TimeoutSeconds", 120).ToString())
        .WithEnvironment("SimulatorSettings__DashboardUrl", dashboardUrl)
        .WithEnvironment("TriggerSettings__Enabled", triggersEnabled.ToString())
        .WithEnvironment("TriggerSettings__ApiBaseUrl", "http://localhost:5090")
        .WithUrl(simulatorUrl + "/", "Simulator")
        .WithUrl(simulatorUrl + "/#triggers", "Triggers");

    // Triggers:Jobs:<name>:Enabled|IntervalSeconds|Hours override the simulator's defaults for that trigger.
    foreach (var job in builder.Configuration.GetSection("Triggers:Jobs").GetChildren())
    {
        foreach (var setting in job.GetChildren())
        {
            simulatorResource.WithEnvironment($"TriggerSettings__Jobs__{job.Key}__{setting.Key}", setting.Value ?? string.Empty);
        }
    }

    if (interceptEnabled)
    {
        // The mocks wait for the simulator and the API for the mocks, so the simulator cannot wait for the API. Its first trigger
        // calls fail until the API is up.
        simulatorFallbackUrl = simulatorUrl + "/intercept/";
        simulator = simulatorResource;
    }
    else
    {
        simulatorResource.WaitFor(api);
    }
}

// mock: the email service sends to a local ACS mock that checks the request signature, answers by recipient address and puts
// delivery reports on the emulator's queue. real: the email service uses the connection string from its own settings.
var acsMode = builder.Configuration["Acs:Mode"] ?? "mock";
if (acsMode == "mock")
{
    // A test-only key. The email service signs with it, and the mock rejects requests that are not signed with it.
    const string acsAccessKey = "YWx0aW5uLW5vdGlmaWNhdGlvbnMtYWNzLW1vY2stdGVzdC1rZXk=";
    var acsPort = builder.Configuration.GetValue("Acs:Port", 5095);
    var acs = builder.AddProject("acs", "../src/Altinn.Notifications.Tools.AcsMock/Altinn.Notifications.Tools.AcsMock.csproj", launchProfileName: null)
        .WithEnvironment("AcsMockSettings__Port", acsPort.ToString())
        .WithEnvironment("AcsMockSettings__AccessKey", acsAccessKey)
        .WithEnvironment("AcsMockSettings__FallbackUrl", simulatorFallbackUrl)
        .WaitFor(serviceBus);

    if (simulator is not null)
    {
        acs.WaitFor(simulator);
    }

    email.WithEnvironment("CommunicationServicesSettings__ConnectionString", "endpoint=http://localhost:" + acsPort + "/;accesskey=" + acsAccessKey)
        .WaitFor(acs);
}
else if (acsMode != "real")
{
    throw new InvalidOperationException($"Acs:Mode must be 'mock' or 'real', not '{acsMode}'.");
}

// The API's calls to Profile, Register, Authorization, Maskinporten and condition endpoints go to local mocks. A system that is
// turned off keeps the endpoint from the API's own settings, e.g. a local altinn-profile on 5030.
(string Name, int Port, string ApiSetting, string ApiPath)[] platformMocks =
[
    ("Profile", 5131, "PlatformSettings__ApiProfileEndpoint", "/profile/api/v1/"),
    ("Register", 5121, "PlatformSettings__ApiRegisterEndpoint", "/register/api/v1/"),
    ("Authorization", 5151, "PlatformSettings__ApiAuthorizationEndpoint", "/authorization/api/v1/"),
    ("Conditions", 5199, string.Empty, string.Empty), // Orders point to these themselves.
    ("Maskinporten", 5197, string.Empty, string.Empty), // See below.
];

if (builder.Configuration.GetValue("Mocks:Enabled", true) && platformMocks.Any(m => builder.Configuration.GetValue($"Mocks:{m.Name}:Enabled", true)))
{
    var mocks = builder.AddProject("platform-mocks", "../src/Altinn.Notifications.Tools.PlatformMocks/Altinn.Notifications.Tools.PlatformMocks.csproj", launchProfileName: null)
        .WithEnvironment("PlatformMocksSettings__FallbackUrl", simulatorFallbackUrl);

    if (simulator is not null)
    {
        mocks.WaitFor(simulator);
    }

    foreach (var (name, defaultPort, apiSetting, apiPath) in platformMocks)
    {
        bool enabled = builder.Configuration.GetValue($"Mocks:{name}:Enabled", true);
        int port = builder.Configuration.GetValue($"Mocks:{name}:Port", defaultPort);
        mocks.WithEnvironment($"PlatformMocksSettings__{name}__Enabled", enabled.ToString())
            .WithEnvironment($"PlatformMocksSettings__{name}__Port", port.ToString());

        if (enabled && apiSetting.Length > 0)
        {
            api.WithEnvironment(apiSetting, "http://localhost:" + port + apiPath);
        }
    }

    if (builder.Configuration.GetValue("Mocks:Conditions:Enabled", true))
    {
        int conditionsPort = builder.Configuration.GetValue("Mocks:Conditions:Port", 5199);
        foreach (string condition in new[] { "true", "false", "corrupt", "error" })
        {
            mocks.WithUrl("http://localhost:" + conditionsPort + "/condition/" + condition, "Condition " + condition);
        }
    }

    // The condition client gets its token from the Maskinporten mock instead of test.maskinporten.no. The grant still has to be
    // signed, so each run makes a key of its own. The mock does not check it.
    if (builder.Configuration.GetValue("Mocks:Maskinporten:Enabled", true))
    {
        string maskinportenUrl = "http://localhost:" + builder.Configuration.GetValue("Mocks:Maskinporten:Port", 5197);
        api.WithEnvironment("SendConditionClient__MaskinportenSettings__TokenEndpoint", maskinportenUrl + "/token")
            .WithEnvironment("SendConditionClient__MaskinportenSettings__AssertionAudience", maskinportenUrl + "/")
            .WithEnvironment("SendConditionClient__MaskinportenSettings__ClientId", "altinn-notifications-local")
            .WithEnvironment("SendConditionClient__MaskinportenSettings__EncodedJwk", CreateSigningKey());
    }

    api.WaitFor(mocks);
}

AddTests("api-tests", api, "api/Altinn.Notifications.API.slnx");
AddTests("email-tests", email, "email-service/Altinn.Notifications.Email.slnx");
AddTests("sms-tests", sms, "sms-service/Altinn.Notifications.Sms.slnx");

var app = builder.Build();

HashSet<string> pendingRestarts = [];
List<FileSystemWatcher> watchers =
[
    WatchSource(app.Services, "api/src", api),
    WatchSource(app.Services, "email-service/src", email),
    WatchSource(app.Services, "sms-service/src", sms),
    WatchSource(app.Services, "shared/src", api, email, sms),
];

app.Run();

foreach (var watcher in watchers)
{
    watcher.Dispose();
}

// A throwaway RSA key as a base64-encoded JWK, the format MaskinportenSettings:EncodedJwk takes.
static string CreateSigningKey()
{
    using var rsa = RSA.Create(2048);
    var key = rsa.ExportParameters(includePrivateParameters: true);
    var jwk = new Dictionary<string, string>
    {
        ["kty"] = "RSA",
        ["kid"] = "altinn-notifications-local",
        ["alg"] = "RS256",
        ["use"] = "sig",
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

void StartContainer(string workingDirectory, string[] runtimeArgs, Dictionary<string, string>? environment = null)
{
    var startInfo = new ProcessStartInfo(containerRuntime, runtimeArgs) { WorkingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory) };
    foreach (var (key, value) in environment ?? [])
    {
        startInfo.Environment[key] = value;
    }

    using var runtime = Process.Start(startInfo)!;
    runtime.WaitForExit();
    if (runtime.ExitCode != 0)
    {
        throw new InvalidOperationException($"{containerRuntime} {string.Join(' ', runtimeArgs)} failed");
    }
}

// No launch profiles: email and sms hardcode ASPNETCORE_URLS, which collides with Aspire's.
// Fixed, unproxied ports because the services call each other on hardcoded localhost URLs.
IResourceBuilder<ProjectResource> AddService(string name, string projectPath, int port, string swaggerPath, string clusterService)
{
    string swaggerUrl = $"http://localhost:{port}/{swaggerPath}";

    var service = builder.AddProject(name, projectPath, launchProfileName: null)
        .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
        .WithUrl(swaggerUrl, "Swagger")
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

    AddClusterSwitch(service, clusterService, port);
    return service;
}

void AddClusterSwitch(IResourceBuilder<ProjectResource> service, string clusterService, int port)
{
    Process? forward = null;

    service.WithCommand(
        "use-cluster",
        "Use cluster",
        async context =>
        {
            await context.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(service.Resource, KnownResourceCommands.StopCommand, context.CancellationToken);

            var logger = context.Services.GetRequiredService<ResourceLoggerService>().GetLogger(service.Resource);
            forward = StartPortForward(clusterService, port, logger);
            var started = forward;
            context.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => started.Kill());

            await context.Services.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(service.Resource, s => s with { State = new ResourceStateSnapshot("Cluster", KnownResourceStateStyles.Info) });
            return CommandResults.Success();
        },
        new CommandOptions { IconName = "Cloud", UpdateState = _ => forward is null ? ResourceCommandState.Enabled : ResourceCommandState.Hidden });

    service.WithCommand(
        "use-local",
        "Use local",
        async context =>
        {
            forward?.Kill();
            forward = null;
            return await context.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(service.Resource, KnownResourceCommands.StartCommand, context.CancellationToken);
        },
        new CommandOptions { IconName = "Desktop", UpdateState = _ => forward is null ? ResourceCommandState.Hidden : ResourceCommandState.Enabled });
}

static Process StartPortForward(string clusterService, int port, ILogger logger)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo("kubectl", ["port-forward", $"service/{clusterService}", $"{port}:80"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        },
        EnableRaisingEvents = true,
    };

    process.OutputDataReceived += (_, e) =>
    {
        if (e.Data is not null)
        {
            logger.LogInformation("{Line}", e.Data);
        }
    };
    process.ErrorDataReceived += (_, e) =>
    {
        if (e.Data is not null)
        {
            logger.LogError("{Line}", e.Data);
        }
    };
    process.Exited += (_, _) => logger.LogWarning("kubectl port-forward exited with code {ExitCode}", process.ExitCode);

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

void AddTests(string name, IResourceBuilder<ProjectResource> service, string solution)
{
    var tests = builder.AddExecutable(name, "dotnet", ".", "test", $"{Components}/{solution}")
        .WithExplicitStart()
        .WithParentRelationship(service.Resource);

    builder.Eventing.Subscribe<BeforeStartEvent>(async (e, ct) =>
    {
        var notifications = e.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.PublishUpdateAsync(tests.Resource, s => s with { IsHidden = true });

        var stopping = e.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        _ = ShowTestResultAsync(notifications, tests.Resource, stopping);
    });

    service.WithCommand(
        "run-tests",
        "Run tests",
        async context =>
        {
            await context.Services.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(tests.Resource, s => s with { IsHidden = false });
            return await context.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(tests.Resource, KnownResourceCommands.StartCommand, context.CancellationToken);
        },
        new CommandOptions { IconName = "Beaker", IsHighlighted = false });
}

// .sql is left out because the API build regenerates a .sql file under src on every run.
FileSystemWatcher WatchSource(IServiceProvider services, string folder, params IResourceBuilder<ProjectResource>[] affected)
{
    string[] extensions = [".cs", ".csproj", ".json"];
    var watcher = new FileSystemWatcher(Path.Combine(builder.AppHostDirectory, Components, folder)) { IncludeSubdirectories = true };

    FileSystemEventHandler onChange = (_, e) =>
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        if (!extensions.Contains(Path.GetExtension(e.FullPath)) || e.FullPath.Contains($"{separator}bin{separator}") || e.FullPath.Contains($"{separator}obj{separator}"))
        {
            return;
        }

        foreach (var service in affected)
        {
            _ = NotifyChangedAsync(services, service.Resource);
        }
    };

    watcher.Changed += onChange;
    watcher.Created += onChange;
    watcher.Deleted += onChange;
    watcher.Renamed += (sender, e) => onChange(sender, e);
    watcher.EnableRaisingEvents = true;
    return watcher;
}

async Task NotifyChangedAsync(IServiceProvider services, ProjectResource service)
{
    lock (pendingRestarts)
    {
        if (!pendingRestarts.Add(service.Name))
        {
            return;
        }
    }

    await Task.Delay(TimeSpan.FromSeconds(2));

    var result = await services.GetRequiredService<IInteractionService>().PromptNotificationAsync(
        $"{service.Name} has changed",
        "Restart it to pick up the changes.",
        new NotificationInteractionOptions { Intent = MessageIntent.Warning, PrimaryButtonText = "Restart" });

    lock (pendingRestarts)
    {
        pendingRestarts.Remove(service.Name);
    }

    if (!result.Canceled && result.Data)
    {
        await services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(service, KnownResourceCommands.RestartCommand);
    }
}

async Task ShowTestResultAsync(ResourceNotificationService notifications, IResource tests, CancellationToken cancellationToken)
{
    await foreach (var update in notifications.WatchAsync(cancellationToken))
    {
        string? state = update.Snapshot.State?.Text;
        if (update.Resource != tests || update.Snapshot.ExitCode is not int exitCode || (state != KnownResourceStates.Finished && state != KnownResourceStates.Exited))
        {
            continue;
        }

        var result = exitCode == 0
            ? new ResourceStateSnapshot("Passed", KnownResourceStateStyles.Success)
            : new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error);

        await notifications.PublishUpdateAsync(tests, s => s with { State = result });
    }
}
