using Altinn.Notifications.Tools.TestHarness;
using Altinn.Notifications.Tools.TestHarness.Configuration;
using Altinn.Notifications.Tools.TestHarness.Mirror;
using Altinn.Notifications.Tools.TestHarness.Triggers;

using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Trace;

// appsettings.json, the GUI and the templates are in the output folder, so they are found whichever folder the control panel is started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Configuration
    .AddUserSecrets<TestHarnessSettings>(optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<TestHarnessSettings>(builder.Configuration.GetSection(nameof(TestHarnessSettings)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RequestStore>();
builder.Services.AddSingleton<TemplateStore>();

// What the mocks send to /mirror, for the Phone tab.
builder.Services.AddSingleton(services => new MirrorStores(
    services.GetRequiredService<IOptions<TestHarnessSettings>>().Value.MirrorSize,
    services.GetRequiredService<TimeProvider>()));

// The scheduler that calls the API's trigger endpoints, like the cron jobs in the cluster.
builder.Services.Configure<TriggerSettings>(builder.Configuration.GetSection(nameof(TriggerSettings)));
builder.Services.AddHttpClient(TriggerScheduler.HttpClientName, (services, client) =>
{
    var settings = services.GetRequiredService<IOptions<TriggerSettings>>().Value;
    client.BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
});
builder.Services.AddSingleton<TriggerScheduler>();
builder.Services.AddHostedService(services => services.GetRequiredService<TriggerScheduler>());

// Aspire sets OTEL_EXPORTER_OTLP_ENDPOINT. The mocks forward traceparent, so the held request shows up in the caller's trace.
if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options => options.Filter = context => context.Request.Path.StartsWithSegments("/intercept")))
        .UseOtlpExporter();
}

var port = builder.Configuration.GetValue("TestHarnessSettings:Port", 5096);
builder.WebHost.UseUrls($"http://localhost:{port}");

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapInterceptEndpoints();
app.MapMirrorEndpoints();
app.MapTriggerEndpoints();
app.Run();
