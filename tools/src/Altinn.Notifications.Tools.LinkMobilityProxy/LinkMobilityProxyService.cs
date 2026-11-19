using System.Diagnostics;
using System.Text;

using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;
using Altinn.Notifications.Tools.Shared;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WireMock;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// Runs a WireMock server in front of LinkMobility that either forwards to <see cref="LinkMobilityProxySettings.Target"/>
/// or answers sends itself by <see cref="SmsScenario"/> and sends delivery reports for them, and logs every request and response
/// with <see cref="PayloadMasker"/> applied.
/// </summary>
internal sealed class LinkMobilityProxyService(
    IOptions<LinkMobilityProxySettings> options,
    DeliveryReportService deliveryReports,
    SmsMirror mirror,
    ILogger<LinkMobilityProxyService> logger) : IHostedService
{
    internal static readonly ActivitySource ActivitySource = new("Altinn.Notifications.Tools.LinkMobilityProxy");

    // Below the send mapping, which has the default priority 0.
    private const int FallbackPriority = 1000;

    private readonly LinkMobilityProxySettings _settings = options.Value;

    // The control panel holds a request until an operator answers or it times out itself, so the client does not time out first.
    private readonly HttpClient _fallbackClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    private WireMockServer? _server;

    private enum ProxyMode
    {
        Stub,
        Interactive,
        Proxy,
    }

    private ProxyMode Mode
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_settings.Target))
            {
                return ProxyMode.Proxy;
            }

            return _settings.Interactive ? ProxyMode.Interactive : ProxyMode.Stub;
        }
    }

    // Lower case in the log, like the README.
    private string ModeName => Mode.ToString().ToLowerInvariant();

    private bool HasFallback => !string.IsNullOrWhiteSpace(_settings.FallbackUrl);

    /// <summary>
    /// The URL the proxy listens on, once started.
    /// </summary>
    internal string? Url => _server?.Url;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Mode == ProxyMode.Interactive && !HasFallback)
        {
            throw new InvalidOperationException("LinkMobilityProxySettings:Interactive needs a FallbackUrl, e.g. the test harness control panel's http://localhost:5096/intercept/.");
        }

        _server = WireMockServer.Start(new WireMockServerSettings
        {
            Urls = [$"http://localhost:{_settings.Port}"],
            StartAdminInterface = false,
            MaxRequestLogCount = 100,
        });

        if (Mode == ProxyMode.Proxy)
        {
            _server
                .Given(Request.Create().WithPath("/*"))
                .RespondWith(Response.Create().WithProxy(_settings.Target));
        }
        else
        {
            // The gateway client posts every send to the root of SmsGatewaySettings:Endpoint.
            _server
                .Given(Request.Create().UsingPost().WithPath("/"))
                .RespondWith(Response.Create().WithCallback(SendAsync));

            // Without a fallback WireMock answers 404 itself.
            if (HasFallback)
            {
                _server
                    .Given(Request.Create().WithPath("/*"))
                    .AtPriority(FallbackPriority)
                    .RespondWith(Response.Create().WithCallback(ForwardAsync));
            }
        }

        _server.LogEntriesChanged += (_, e) =>
        {
            foreach (ILogEntry entry in e.NewItems ?? Array.Empty<ILogEntry>())
            {
                LogEntry(entry);
            }
        };

        string target = Mode switch
        {
            ProxyMode.Proxy => $" to {_settings.Target}",
            _ when HasFallback => $" with fallback to {_settings.FallbackUrl}",
            _ => string.Empty,
        };

        logger.LogInformation(
            "LinkMobility proxy listening on {Url} in {Mode} mode{Target}, delivery reports {DeliveryReports}",
            _server.Url,
            ModeName,
            target,
            Mode != ProxyMode.Proxy && deliveryReports.IsEnabled ? $"to {_settings.DeliveryReportUrl}" : "off");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server?.Stop();
        _server?.Dispose();
        _fallbackClient.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Answers a send by the scenario of each recipient, or in interactive mode lets an operator answer it,
    /// and queues a delivery report for each message the answer accepted.
    /// </summary>
    private async Task<IResponseMessage> SendAsync(IRequestMessage request)
    {
        if (!GatewayXml.TryReadSend(BodyText(request.BodyData), out var messages))
        {
            return HasFallback ? await ForwardAsync(request) : Xml(404, "Not a LinkMobility send request.");
        }

        IResponseMessage response;
        List<SmsResult> accepted;
        List<SmsResult> results;
        if (_settings.Interactive)
        {
            response = await ForwardAsync(request);
            accepted = GatewayXml.ReadAccepted(BodyText(response.BodyData));
            results = messages
                .Select(m => accepted.FirstOrDefault(r => r.Id == m.Id) ?? new SmsResult(m.Id, null, $"Not accepted: {response.StatusCode}"))
                .ToList();
        }
        else
        {
            results = messages
                .Select(m => SmsScenarios.Pick(m.Recipient) == SmsScenario.InvalidRecipient
                    ? new SmsResult(m.Id, null, "Invalid RCV")
                    : new SmsResult(m.Id, Guid.NewGuid().ToString()))
                .ToList();

            response = Xml(200, GatewayXml.SendResponse(results));
            accepted = results.Where(r => r.Reference is not null).ToList();
        }

        foreach (var (message, result) in messages.Zip(results))
        {
            mirror.Sent(message, result, SmsScenarios.Pick(message.Recipient));
        }

        foreach (var result in accepted)
        {
            var message = messages.FirstOrDefault(m => m.Id == result.Id);
            if (message is not null)
            {
                deliveryReports.Queue(new PendingDeliveryReport(
                    result.Reference!,
                    message.Recipient,
                    SmsScenarios.Pick(message.Recipient),
                    Header(request.Headers, "traceparent"),
                    Header(request.Headers, "tracestate")));
            }
        }

        return response;
    }

    /// <summary>
    /// Sends a request to <c>{FallbackUrl}linkmobility/{path}</c>, so an operator can answer it in the test harness control panel.
    /// </summary>
    private Task<IResponseMessage> ForwardAsync(IRequestMessage request)
    {
        string url = _settings.FallbackUrl.TrimEnd('/') + "/linkmobility" + request.Path + request.RawQuery;
        return WireMockForwarder.ForwardAsync(_fallbackClient, url, request, error => Xml(502, error));
    }

    private static ResponseMessage Xml(int statusCode, string body)
    {
        var message = new ResponseMessage
        {
            StatusCode = statusCode,
            BodyData = new BodyData { BodyAsString = body, DetectedBodyType = BodyType.String, Encoding = Encoding.UTF8 },
        };

        message.AddHeader("Content-Type", statusCode == 200 ? "text/xml" : "text/plain");
        return message;
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

        string? requestBody = BodyText(request.BodyData);
        string scenarios = Mode != ProxyMode.Proxy && GatewayXml.TryReadSend(requestBody, out var messages)
            ? string.Join(", ", messages.Select(m => SmsScenarios.Pick(m.Recipient)))
            : "-";

        logger.LogInformation(
            "LinkMobility {Mode} {Method} {Path} -> {StatusCode} ({Scenario})\nRequest: {RequestBody}\nResponse: {ResponseBody}",
            ModeName,
            request.Method,
            request.Path,
            response.StatusCode,
            scenarios,
            PayloadMasker.Mask(requestBody),
            PayloadMasker.Mask(BodyText(response.BodyData)));
    }

    // WireMock keeps a body as a string or as bytes, depending on what it detected.
    private static string? BodyText(IBodyData? body)
    {
        return body?.BodyAsString ?? (body?.BodyAsBytes is { } bytes ? Encoding.UTF8.GetString(bytes) : null);
    }

    private static string? Header(IDictionary<string, WireMockList<string>>? headers, string name)
    {
        return headers is not null && headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
    }
}
