using Altinn.Notifications.Tools.TestHarness;
using Altinn.Notifications.Tools.TestHarness.Configuration;

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
app.Run();
