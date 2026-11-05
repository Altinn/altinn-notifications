using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.Tools.PlatformMocks.Configuration;
using Altinn.Notifications.Tools.PlatformMocks.Mocks;

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

namespace Altinn.Notifications.Tools.PlatformMocks;

/// <summary>
/// Starts one WireMock server for each enabled platform system, and logs every request and response.
/// </summary>
internal sealed class PlatformMocksService(IOptions<PlatformMocksSettings> options, ILogger<PlatformMocksService> logger) : IHostedService
{
    internal static readonly ActivitySource ActivitySource = new("Altinn.Notifications.Tools.PlatformMocks");

    // Below every mapping the mocks add, which have the default priority 0.
    private const int FallbackPriority = 1000;

    private static readonly string[] _skippedRequestHeaders = ["Host", "Content-Length", "Transfer-Encoding", "Connection"];

    private readonly PlatformMocksSettings _settings = options.Value;
    private readonly Dictionary<string, WireMockServer> _servers = [];

    // The simulator holds a request until an operator answers or it times out itself, so the client does not time out first.
    private readonly HttpClient _fallbackClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// The URL of each running server, by system name.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Urls => _servers.ToDictionary(s => s.Key, s => s.Value.Url!);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Start(_settings.Profile, new ProfileMock(_settings.Profile));
        Start(_settings.Register, new RegisterMock());
        Start(_settings.Authorization, new AuthorizationMock(_settings.Authorization));
        Start(_settings.Conditions, new ConditionsMock());
        Start(_settings.Maskinporten, new MaskinportenMock());

        if (_servers.Count == 0)
        {
            logger.LogWarning("All platform mocks are disabled");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var server in _servers.Values)
        {
            server.Stop();
            server.Dispose();
        }

        _servers.Clear();
        _fallbackClient.Dispose();
        return Task.CompletedTask;
    }

    private void Start(MockServerSettings serverSettings, IPlatformMock mock)
    {
        if (!serverSettings.Enabled)
        {
            logger.LogInformation("{System} mock is disabled", mock.System);
            return;
        }

        var server = WireMockServer.Start(new WireMockServerSettings
        {
            Urls = [$"http://localhost:{serverSettings.Port}"],
            StartAdminInterface = false,
            MaxRequestLogCount = 100,
        });

        mock.AddMappings(server);
        AddFallback(server, mock.System);

        server.LogEntriesChanged += (_, e) =>
        {
            foreach (ILogEntry entry in e.NewItems ?? Array.Empty<ILogEntry>())
            {
                LogEntry(mock.System, entry);
            }
        };

        _servers[mock.System] = server;
        logger.LogInformation("{System} mock listening on {Url}", mock.System, server.Url);
    }

    /// <summary>
    /// Sends what no mapping matches to <c>{FallbackUrl}{system}/{path}</c>, so an operator can answer it in the simulator.
    /// Without a fallback URL WireMock answers 404 itself.
    /// </summary>
    private void AddFallback(WireMockServer server, string system)
    {
        if (string.IsNullOrWhiteSpace(_settings.FallbackUrl))
        {
            return;
        }

        string baseUrl = _settings.FallbackUrl.TrimEnd('/') + "/" + system;
        server
            .Given(Request.Create().WithPath("/*"))
            .AtPriority(FallbackPriority)
            .RespondWith(Response.Create().WithCallback(request => ForwardAsync(baseUrl, request)));
    }

    private async Task<IResponseMessage> ForwardAsync(string baseUrl, IRequestMessage request)
    {
        using var forward = new HttpRequestMessage(new HttpMethod(request.Method), baseUrl + request.Path + request.RawQuery);
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
            return new ResponseMessage
            {
                StatusCode = 502,
                BodyData = new BodyData { BodyAsString = $"Fallback to {forward.RequestUri} failed: {ex.Message}", DetectedBodyType = BodyType.String },
            };
        }
    }

    private void LogEntry(string system, ILogEntry entry)
    {
        var request = entry.RequestMessage;
        var response = entry.ResponseMessage;
        if (request is null || response is null)
        {
            return;
        }

        // Continue the caller's trace, so the entry shows up under the API's span in the dashboard.
        ActivityContext.TryParse(Header(request.Headers, "traceparent"), Header(request.Headers, "tracestate"), out var parent);
        using var activity = ActivitySource.StartActivity($"{system} {request.Method}", ActivityKind.Server, parent, startTime: request.DateTime);
        activity?.SetTag("http.response.status_code", response.StatusCode);

        // Only test data passes through the mocks, so the payloads are logged as they are.
        logger.LogInformation(
            "{System} {Method} {Path} -> {StatusCode}\nRequest: {RequestBody}\nResponse: {ResponseBody}",
            system,
            request.Method,
            request.Path,
            response.StatusCode,
            BodyText(request.BodyData),
            BodyText(response.BodyData));
    }

    // WireMock keeps a body as a string, parsed JSON or bytes, depending on what it detected.
    private static string? BodyText(IBodyData? body)
    {
        return body?.BodyAsString
            ?? (body?.BodyAsJson is { } json ? JsonSerializer.Serialize(json) : null)
            ?? (body?.BodyAsBytes is { } bytes ? Encoding.UTF8.GetString(bytes) : null);
    }

    private static string? Header(IDictionary<string, WireMockList<string>>? headers, string name)
    {
        return headers is not null && headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
    }
}
