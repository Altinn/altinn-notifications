using System.Diagnostics;
using System.Globalization;
using System.Text;

using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

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
/// with <see cref="PayloadMasker"/> applied, or with <see cref="PlainPayload"/> when <see cref="LinkMobilityProxySettings.LogPayloads"/> is Plain.
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

    private static readonly string[] _skippedRequestHeaders = ["Host", "Content-Length", "Transfer-Encoding", "Connection"];

    private readonly LinkMobilityProxySettings _settings = options.Value;

    // The control panel holds a request until an operator answers or it times out itself, so the client does not time out first.
    private readonly HttpClient _fallbackClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    private WireMockServer? _server;

    private int _payloadCount;

    private string Mode => !string.IsNullOrWhiteSpace(_settings.Target) ? "proxy" : _settings.Interactive ? "interactive" : "stub";

    private bool HasFallback => !string.IsNullOrWhiteSpace(_settings.FallbackUrl);

    private bool HasPayloadFolder => !string.IsNullOrWhiteSpace(_settings.PayloadFolder);

    /// <summary>
    /// The URL the proxy listens on, once started.
    /// </summary>
    internal string? Url => _server?.Url;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Mode == "interactive" && !HasFallback)
        {
            throw new InvalidOperationException("LinkMobilityProxySettings:Interactive needs a FallbackUrl, e.g. the test harness control panel's http://localhost:5096/intercept/.");
        }

        string[] urls = [$"http://localhost:{_settings.Port}"];
        EnsurePayloadLoggingIsLocal(_settings, urls);
        if (HasPayloadFolder)
        {
            Directory.CreateDirectory(_settings.PayloadFolder);
        }

        _server = WireMockServer.Start(new WireMockServerSettings
        {
            Urls = urls,
            StartAdminInterface = false,
            MaxRequestLogCount = 100,
        });

        if (Mode == "proxy")
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

        logger.LogInformation(
            "LinkMobility proxy listening on {Url} in {Mode} mode{Target}, delivery reports {DeliveryReports}",
            _server.Url,
            Mode,
            Mode == "proxy" ? $" to {_settings.Target}" : HasFallback ? $" with fallback to {_settings.FallbackUrl}" : string.Empty,
            Mode != "proxy" && deliveryReports.IsEnabled ? $"to {_settings.DeliveryReportUrl}" : "off");

        if (_settings.LogPayloads == PayloadLogMode.Plain)
        {
            logger.LogWarning(
                "LinkMobility proxy logs payloads in plain text: recipient numbers and message texts, which can hold one-time codes, are not masked. Only <PW> is. Byte-exact copies go to {PayloadFolder}. Set LinkMobilityProxySettings:LogPayloads to Masked when done.",
                HasPayloadFolder ? Path.GetFullPath(_settings.PayloadFolder) : "no folder");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Refuses payload settings that could expose recipient numbers and message texts: <see cref="PayloadLogMode.Plain"/> only
    /// when every URL the proxy listens on is loopback, and <see cref="LinkMobilityProxySettings.PayloadFolder"/> only with Plain.
    /// </summary>
    internal static void EnsurePayloadLoggingIsLocal(LinkMobilityProxySettings settings, IEnumerable<string> urls)
    {
        if (settings.LogPayloads == PayloadLogMode.Plain
            && urls.FirstOrDefault(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsLoopback) is { } exposed)
        {
            throw new InvalidOperationException(
                $"LinkMobilityProxySettings:LogPayloads=Plain logs recipient numbers and message texts, so it needs the proxy to listen on localhost only, not on {exposed}.");
        }

        if (!string.IsNullOrWhiteSpace(settings.PayloadFolder) && settings.LogPayloads != PayloadLogMode.Plain)
        {
            throw new InvalidOperationException("LinkMobilityProxySettings:PayloadFolder needs LogPayloads=Plain.");
        }
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
    private async Task<IResponseMessage> ForwardAsync(IRequestMessage request)
    {
        string url = _settings.FallbackUrl.TrimEnd('/') + "/linkmobility" + request.Path + request.RawQuery;
        using var forward = new HttpRequestMessage(new HttpMethod(request.Method), url);
        if (request.BodyAsBytes is { Length: > 0 } body)
        {
            forward.Content = new ByteArrayContent(body);
        }

        foreach (var (name, values) in request.Headers ?? new Dictionary<string, WireMockList<string>>())
        {
            if (_skippedRequestHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!forward.Headers.TryAddWithoutValidation(name, values.ToArray()))
            {
                forward.Content?.Headers.TryAddWithoutValidation(name, values.ToArray());
            }
        }

        try
        {
            using var response = await _fallbackClient.SendAsync(forward);
            var message = new ResponseMessage
            {
                StatusCode = (int)response.StatusCode,
                BodyData = new BodyData { BodyAsBytes = await response.Content.ReadAsByteArrayAsync(), DetectedBodyType = BodyType.Bytes },
            };

            foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
            {
                if (!name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    message.AddHeader(name, values.ToArray());
                }
            }

            return message;
        }
        catch (HttpRequestException ex)
        {
            return Xml(502, $"Fallback to {url} failed: {ex.Message}");
        }
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
        string scenarios = Mode != "proxy" && GatewayXml.TryReadSend(requestBody, out var messages)
            ? string.Join(", ", messages.Select(m => SmsScenarios.Pick(m.Recipient)))
            : "-";

        if (_settings.LogPayloads == PayloadLogMode.Plain)
        {
            LogPlain(request, response, scenarios);
            return;
        }

        logger.LogInformation(
            "LinkMobility {Mode} {Method} {Path} -> {StatusCode} ({Scenario})\nRequest: {RequestBody}\nResponse: {ResponseBody}",
            Mode,
            request.Method,
            request.Path,
            response.StatusCode,
            scenarios,
            PayloadMasker.Mask(requestBody),
            PayloadMasker.Mask(BodyText(response.BodyData)));
    }

    private void LogPlain(IRequestMessage request, IResponseMessage response, string scenarios)
    {
        byte[] requestBody = BodyBytes(request.BodyData);
        byte[] responseBody = BodyBytes(response.BodyData);
        string? requestType = Header(request.Headers, "Content-Type");
        string? responseType = Header(response.Headers, "Content-Type");

        logger.LogInformation(
            "LinkMobility {Mode} {Method} {Path} -> {StatusCode} ({Scenario})\n{Request}\n{Response}{Files}",
            Mode,
            request.Method,
            request.Path,
            response.StatusCode,
            scenarios,
            PlainPayload.Describe("Request", requestBody, requestType),
            PlainPayload.Describe("Response", responseBody, responseType),
            WritePayloads(request.DateTime, requestBody, requestType, responseBody, responseType));
    }

    /// <summary>
    /// Writes the request and response bodies with only the password masked to <see cref="LinkMobilityProxySettings.PayloadFolder"/>,
    /// named by time, a running number and the gateway references, and returns a line with their paths for the log.
    /// </summary>
    private string WritePayloads(DateTime time, byte[] requestBody, string? requestType, byte[] responseBody, string? responseType)
    {
        if (!HasPayloadFolder)
        {
            return string.Empty;
        }

        var request = PlainPayload.MaskedBytes(requestBody, requestType);
        var response = PlainPayload.MaskedBytes(responseBody, responseType);
        if (request is null || response is null)
        {
            return "\nFiles: [left out: the password could not be masked in the bytes]";
        }

        var references = GatewayXml.ReadAccepted(PlainPayload.Encoding(responseType).GetString(response)).Select(r => r.Reference!).ToList();
        string name = string.Join(
            '-',
            time.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture),
            Interlocked.Increment(ref _payloadCount).ToString("D4", CultureInfo.InvariantCulture),
            references.Count == 0 ? "no-ref" : "ref-" + string.Join('+', references.Take(3).Select(SafeFileName)));

        try
        {
            string requestPath = Path.GetFullPath(Path.Combine(_settings.PayloadFolder, name + "-request.xml"));
            string responsePath = Path.GetFullPath(Path.Combine(_settings.PayloadFolder, name + "-response.xml"));
            File.WriteAllBytes(requestPath, request);
            File.WriteAllBytes(responsePath, response);
            return $"\nFiles: {requestPath}\n       {responsePath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"\nFiles: [not written: {ex.Message}]";
        }
    }

    private static string SafeFileName(string value)
    {
        return string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    }

    // WireMock keeps a body as a string or as bytes, depending on what it detected.
    private static string? BodyText(IBodyData? body)
    {
        return body?.BodyAsString ?? (body?.BodyAsBytes is { } bytes ? Encoding.UTF8.GetString(bytes) : null);
    }

    // The bytes as they went over the wire. A body WireMock only has as a string, such as the stub's own answers, is encoded
    // as WireMock writes it.
    private static byte[] BodyBytes(IBodyData? body)
    {
        return body?.BodyAsBytes ?? (body?.BodyAsString is { } text ? (body.Encoding ?? Encoding.UTF8).GetBytes(text) : []);
    }

    private static string? Header(IDictionary<string, WireMockList<string>>? headers, string name)
    {
        return headers?.FirstOrDefault(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
    }
}
