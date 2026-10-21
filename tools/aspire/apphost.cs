#:sdk Aspire.AppHost.Sdk@13.6.1
#:property AspireUseCliBundle=true

using System.Diagnostics;
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

// Aspire starts all services with dotnet run at once, and their parallel restores and
// builds of the shared project collide. Building them one by one first avoids that.
foreach (var project in projects)
{
    using var build = Process.Start("dotnet", ["build", Path.Combine(builder.AppHostDirectory, project)]);
    build.WaitForExit();
    if (build.ExitCode != 0)
    {
        throw new InvalidOperationException($"Build failed for {project}");
    }
}

// The dashboard lists resources in the order they are added, so the containers are added last.
var api = AddService("api", projects[0], 5090, "swagger", "altinn-notifications");
var email = AddService("email", projects[1], 5091, "swagger", "altinn-notifications-email");
var sms = AddService("sms", projects[2], 5092, string.Empty, "altinn-notifications-sms");

// The containers live in Podman, not in Aspire, so they keep running when Aspire stops.
// They are started here rather than through a shell so this works on Windows too; both
// commands are no-ops when the containers are already running. The resources only tail
// the container logs so they show up in the dashboard.
StartContainer(".", "start", "notifications-db");
StartContainer("../asb-emulator", "compose", "up", "-d");

var postgres = builder.AddExecutable("postgres", "podman", ".", "logs", "-f", "notifications-db");
var serviceBus = builder.AddExecutable("servicebus", "podman", ".", "logs", "-f", "servicebus-emulator")
    .WithHttpEndpoint(port: 5300, targetPort: 5300, name: "management", isProxied: false)
    .WithHttpHealthCheck("/health", endpointName: "management");

api.WaitFor(postgres).WaitFor(serviceBus);
email.WaitFor(serviceBus);
sms.WaitFor(serviceBus);

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

void StartContainer(string workingDirectory, params string[] podmanArgs)
{
    var startInfo = new ProcessStartInfo("podman", podmanArgs) { WorkingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory) };
    using var podman = Process.Start(startInfo)!;
    podman.WaitForExit();
    if (podman.ExitCode != 0)
    {
        throw new InvalidOperationException($"podman {string.Join(' ', podmanArgs)} failed");
    }
}

// The launch profiles are skipped on purpose: email and sms hardcode ASPNETCORE_URLS
// to localhost:509x, which collides with the 127.0.0.1:509x Aspire injects.
//
// The ports stay fixed and unproxied because the services address each other by
// hardcoded localhost URLs (see PlatformSettings in the API's appsettings.json).
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

// "Use cluster" stops the local service and port-forwards the same port to the service in the
// current kube context instead; "Use local" switches back. kubectl output goes to the service's log.
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

// The tests stay hidden until they are first run from the service's "Run tests" command,
// so they don't clutter the resource list and log filters.
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

// Shows a dashboard notification with a Restart button when a service's source changes.
// Only .cs, .csproj and .json files count: the API build regenerates a .sql file under
// src on every run, which would otherwise raise a new alert after each restart.
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
    // One notification per service at a time; the short delay lets a save of several files settle first.
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

// Replaces the generic "Finished" state with Passed or Failed based on the dotnet test exit code.
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
