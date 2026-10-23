using System.Diagnostics;

using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Types;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// Runs a WireMock server in front of LinkMobility that either forwards to <see cref="LinkMobilityProxySettings.Target"/>
/// or answers with a stub, and logs every request and response with <see cref="PayloadMasker"/> applied.
/// </summary>
internal sealed class LinkMobilityProxyService(IOptions<LinkMobilityProxySettings> options, ILogger<LinkMobilityProxyService> logger) : IHostedService
{
    internal const string StubResponse = """<?xml version="1.0" encoding="ISO-8859-1"?><SESSION><LOGON>OK</LOGON><MSGLST><MSG><ID>1</ID><REF>stub-ref</REF><STATUS>OK</STATUS></MSG></MSGLST></SESSION>""";

    internal static readonly ActivitySource ActivitySource = new("Altinn.Notifications.Tools.LinkMobilityProxy");

    private readonly LinkMobilityProxySettings _settings = options.Value;
    private WireMockServer? _server;

    private bool IsStub => string.IsNullOrWhiteSpace(_settings.Target);

    /// <summary>
    /// The URL the proxy listens on, once started.
    /// </summary>
    internal string? Url => _server?.Url;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server = WireMockServer.Start(new WireMockServerSettings
        {
            Urls = [$"http://localhost:{_settings.Port}"],
            StartAdminInterface = false,
            MaxRequestLogCount = 100,
        });

        var response = Response.Create();
        _server
            .Given(Request.Create().WithPath("/*"))
            .RespondWith(IsStub
                ? response.WithStatusCode(200).WithHeader("Content-Type", "text/xml").WithBody(StubResponse)
                : response.WithProxy(_settings.Target));

        _server.LogEntriesChanged += (_, e) =>
        {
            foreach (ILogEntry entry in e.NewItems ?? Array.Empty<ILogEntry>())
            {
                LogEntry(entry);
            }
        };

        logger.LogInformation("LinkMobility proxy listening on {Url} in {Mode} mode", _server.Url, IsStub ? "stub" : $"proxy to {_settings.Target}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server?.Stop();
        _server?.Dispose();
        return Task.CompletedTask;
    }

    private void LogEntry(ILogEntry entry)
    {
        var request = entry.RequestMessage;
        var response = entry.ResponseMessage;
        if (request is null || response is null)
        {
            return;
        }

        // Continue the caller's trace, so the entry shows up under the SMS service's span in the dashboard.
        ActivityContext.TryParse(Header(request.Headers, "traceparent"), Header(request.Headers, "tracestate"), out var parent);
        using var activity = ActivitySource.StartActivity($"LinkMobility {request.Method}", ActivityKind.Server, parent, startTime: request.DateTime);
        activity?.SetTag("http.response.status_code", response.StatusCode);

        logger.LogInformation(
            "LinkMobility {Mode} {Method} {Path} -> {StatusCode}\nRequest: {RequestBody}\nResponse: {ResponseBody}",
            IsStub ? "stub" : "proxy",
            request.Method,
            request.Path,
            response.StatusCode,
            PayloadMasker.Mask(request.Body),
            PayloadMasker.Mask(response.BodyData?.BodyAsString));
    }

    private static string? Header(IDictionary<string, WireMockList<string>>? headers, string name)
    {
        return headers is not null && headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
    }
}
