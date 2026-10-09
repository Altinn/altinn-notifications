#:sdk Aspire.AppHost.Sdk@13.5.4
#:property AspireUseCliBundle=true

using System.Diagnostics;

const string Components = "../../components";

var builder = DistributedApplication.CreateBuilder(args);

// The containers live in Podman, not in Aspire, so they keep running when Aspire stops.
// The start commands are no-ops when the containers are already running; the log tail
// keeps the resource running so the container output shows up in the dashboard.
var postgres = builder.AddExecutable("postgres", "sh", ".", "-c", "podman start notifications-db && exec podman logs -f notifications-db");
var serviceBus = builder.AddExecutable("servicebus", "sh", "../asb-emulator", "-c", "podman compose up -d && exec podman logs -f servicebus-emulator");

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

AddService("api", projects[0], 5090)
    .WaitFor(postgres)
    .WaitFor(serviceBus);
AddService("email", projects[1], 5091)
    .WaitFor(serviceBus);
AddService("sms", projects[2], 5092)
    .WaitFor(serviceBus);

builder.Build().Run();

// The launch profiles are skipped on purpose: email and sms hardcode ASPNETCORE_URLS
// to localhost:509x, which collides with the 127.0.0.1:509x Aspire injects.
//
// The ports stay fixed and unproxied because the services address each other by
// hardcoded localhost URLs (see PlatformSettings in the API's appsettings.json).
IResourceBuilder<ProjectResource> AddService(string name, string projectPath, int port) =>
    builder.AddProject(name, projectPath, launchProfileName: null)
        .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
