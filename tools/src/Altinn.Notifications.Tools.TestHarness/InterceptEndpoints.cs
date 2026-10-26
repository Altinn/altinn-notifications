using System.Text;
using System.Threading.Channels;

using Altinn.Notifications.Tools.TestHarness.Configuration;
using Altinn.Notifications.Tools.TestHarness.Mirror;
using Altinn.Notifications.Tools.TestHarness.Triggers;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace Altinn.Notifications.Tools.TestHarness;

/// <summary>
/// <c>/intercept/{system}/{path}</c>, where the mocks send what they do not recognise, and the API the GUI uses to answer it.
/// </summary>
internal static class InterceptEndpoints
{
    internal const string InterceptPrefix = "/intercept/";

    private static readonly string[] _skippedResponseHeaders = ["Content-Length", "Transfer-Encoding", "Connection"];

    public static void MapInterceptEndpoints(this WebApplication app)
    {
        app.Map(InterceptPrefix + "{system}/{**path}", InterceptAsync);

        var api = app.MapGroup("/api");
        api.MapGet("/state", GetState);
        api.MapGet("/templates", GetTemplates);
        api.MapPost("/requests/{id}/answer", Answer);
        api.MapPut("/auto-replies/{system}", SetAutoReply);
        api.MapGet("/events", EventsAsync);
    }

    /// <summary>
    /// Holds the request until the operator answers it, an auto-reply does, or the timeout runs out.
    /// </summary>
    private static async Task InterceptAsync(HttpContext context, string system, RequestStore store, TemplateStore templates, IOptions<TestHarnessSettings> options, TimeProvider timeProvider, ILogger<InterceptLog> logger)
    {
        int timeoutSeconds = options.Value.TimeoutSeconds;
        var request = await ReadRequestAsync(context, system.ToLowerInvariant(), timeProvider.GetUtcNow(), timeoutSeconds);

        if (store.GetAutoReply(request.System) is string key && templates.Find(key) is { } template)
        {
            var reply = template.Render(timeProvider.GetUtcNow());
            store.AddHistory(new HistoryEntry(request, reply, Outcome.AutoReplied, timeProvider.GetUtcNow()));
            logger.LogInformation("Intercept {System} {Method} {Path} -> {Status} (auto-reply {Template})", request.System, request.Method, request.Path, reply.Status, key);
            await WriteResponseAsync(context, reply, "auto");
            return;
        }

        logger.LogInformation("Intercept {System} {Method} {Path} held for {Seconds} s", request.System, request.Method, request.Path, timeoutSeconds);
        var answer = store.Hold(request);
        try
        {
            await answer.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), timeProvider, context.RequestAborted);
        }
        catch (TimeoutException)
        {
            store.TryResolve(request.Id, SimulatedResponse.Timeout(timeoutSeconds), Outcome.TimedOut, timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            store.TryResolve(request.Id, null, Outcome.Cancelled, timeProvider.GetUtcNow());
            logger.LogInformation("Intercept {System} {Method} {Path} cancelled by the caller", request.System, request.Method, request.Path);
            return;
        }

        // Set by whichever came first, the operator or the timeout.
        var response = await answer;
        logger.LogInformation("Intercept {System} {Method} {Path} -> {Status}", request.System, request.Method, request.Path, response.Status);
        await WriteResponseAsync(context, response, "operator");
    }

    private static IResult GetState(RequestStore store, TemplateStore templates, IOptions<TestHarnessSettings> options)
    {
        var settings = options.Value;
        var pending = store.Pending;
        var history = store.History;

        string[] systems = [.. templates.GetAll().Select(t => t.System).Where(s => s != TemplateStore.Generic)
            .Concat(pending.Select(r => r.System))
            .Concat(history.Select(h => h.Request.System))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

        return Results.Ok(new
        {
            settings.Intercept,
            settings.TimeoutSeconds,
            Systems = systems,
            store.AutoReplies,
            Pending = pending.Select(r => View(r, settings)),
            History = history.Select(h => new { Request = View(h.Request, settings), h.Response, Outcome = h.Outcome.ToString(), h.HandledAt }),
        });
    }

    /// <summary>
    /// The templates for a system and the generic ones, with <c>{{guid}}</c> and <c>{{now}}</c> filled in.
    /// </summary>
    private static IResult GetTemplates(string? system, TemplateStore templates, TimeProvider timeProvider)
    {
        var list = string.IsNullOrEmpty(system) ? templates.GetAll() : templates.For(system);
        var now = timeProvider.GetUtcNow();
        return Results.Ok(list.Select(t =>
        {
            var rendered = t.Render(now);
            return new { t.Key, t.System, t.Name, rendered.Status, rendered.Headers, rendered.Body };
        }));
    }

    private static IResult Answer(string id, AnswerRequest answer, RequestStore store, TimeProvider timeProvider)
    {
        var headers = answer.Headers ?? [];
        Dictionary<string, string[]> errors = [];
        if (answer.Status is < 100 or > 599)
        {
            errors["status"] = ["Status must be between 100 and 599."];
        }

        foreach (var (name, value) in headers)
        {
            if (name.Length == 0 || name.Any(c => c <= ' ' || c >= 127 || c == ':') || value.Any(c => c is '\r' or '\n'))
            {
                errors[$"headers.{name}"] = ["Header names must be HTTP tokens and values must be on one line."];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var response = new SimulatedResponse(answer.Status, headers, answer.Body ?? string.Empty);
        return store.TryResolve(id, response, Outcome.Answered, timeProvider.GetUtcNow())
            ? Results.NoContent()
            : Results.NotFound(new { error = $"Request {id} is not waiting for an answer. It may have timed out or been cancelled." });
    }

    private static IResult SetAutoReply(string system, AutoReplyRequest request, RequestStore store, TemplateStore templates)
    {
        if (!string.IsNullOrEmpty(request.Template) && templates.Find(request.Template) is null)
        {
            return Results.NotFound(new { error = $"No template {request.Template}." });
        }

        store.SetAutoReply(system.ToLowerInvariant(), request.Template);
        return Results.NoContent();
    }

    /// <summary>
    /// Server-sent events that tell the GUI to fetch <c>/api/state</c>, <c>/api/triggers</c> and <c>/api/mirror/{channel}</c> again.
    /// </summary>
    private static async Task EventsAsync(HttpContext context, RequestStore store, TriggerScheduler scheduler, MirrorStores mirrors)
    {
        var cancellationToken = context.RequestAborted;
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";

        var changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        void OnChanged() => changes.Writer.TryWrite(true);
        store.Changed += OnChanged;
        scheduler.Changed += OnChanged;
        foreach (var mirror in mirrors.All)
        {
            mirror.Changed += OnChanged;
        }

        try
        {
            await WriteEventAsync("data: changed\n\n");
            while (!cancellationToken.IsCancellationRequested)
            {
                using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                heartbeat.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    await changes.Reader.ReadAsync(heartbeat.Token);
                    await WriteEventAsync("data: changed\n\n");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await WriteEventAsync(": ping\n\n");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The GUI went away.
        }
        finally
        {
            store.Changed -= OnChanged;
            scheduler.Changed -= OnChanged;
            foreach (var mirror in mirrors.All)
            {
                mirror.Changed -= OnChanged;
            }
        }

        async Task WriteEventAsync(string text)
        {
            await context.Response.WriteAsync(text, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static async Task<SimulatedRequest> ReadRequestAsync(HttpContext context, string system, DateTimeOffset now, int timeoutSeconds)
    {
        using var stream = new MemoryStream();
        await context.Request.Body.CopyToAsync(stream, context.RequestAborted);

        // The raw target keeps the path and query exactly as the mock forwarded them. The in-memory test server leaves it empty.
        string? rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        string target = rawTarget?.StartsWith(InterceptPrefix, StringComparison.OrdinalIgnoreCase) == true ? rawTarget : context.Request.Path + context.Request.QueryString;
        string prefix = InterceptPrefix + system;
        string path = target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? target[prefix.Length..] : target;
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        string? traceparent = context.Request.Headers["traceparent"].FirstOrDefault();
        string[] traceParts = traceparent?.Split('-') ?? [];

        return new SimulatedRequest(
            Guid.NewGuid().ToString("N"),
            system,
            context.Request.Method,
            path,
            headers,
            EncodingOf(context.Request.ContentType).GetString(stream.ToArray()),
            now,
            now.AddSeconds(timeoutSeconds),
            traceParts.Length == 4 && traceParts[1].Length == 32 ? traceParts[1] : null);
    }

    private static async Task WriteResponseAsync(HttpContext context, SimulatedResponse response, string source)
    {
        context.Response.StatusCode = response.Status;
        foreach (var (name, value) in response.Headers)
        {
            if (!_skippedResponseHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.Headers[name] = value;
            }
        }

        if (!context.Response.Headers.ContainsKey(SimulatedResponse.SimulatorHeader))
        {
            context.Response.Headers[SimulatedResponse.SimulatorHeader] = source;
        }

        byte[] body = EncodingOf(context.Response.ContentType).GetBytes(response.Body);
        await context.Response.Body.WriteAsync(body, context.RequestAborted);
    }

    /// <summary>
    /// The charset of a content type, e.g. iso-8859-1 for LinkMobility, or UTF-8.
    /// </summary>
    private static Encoding EncodingOf(string? contentType)
    {
        if (MediaTypeHeaderValue.TryParse(contentType, out var parsed) && !string.IsNullOrEmpty(parsed.CharSet))
        {
            try
            {
                return Encoding.GetEncoding(parsed.CharSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                // Unknown charset, fall back to UTF-8.
            }
        }

        return Encoding.UTF8;
    }

    private static object View(SimulatedRequest request, TestHarnessSettings settings) => new
    {
        request.Id,
        request.System,
        request.Method,
        request.Path,
        Headers = SecretMasker.MaskHeaders(request.Headers),
        Body = SecretMasker.MaskBody(request.Body),
        request.ReceivedAt,
        request.ExpiresAt,
        request.TraceId,
        TraceUrl = request.TraceId is not null && settings.DashboardUrl.Length > 0 ? $"{settings.DashboardUrl.TrimEnd('/')}/traces/detail/{request.TraceId}" : null,
    };

    internal sealed record AnswerRequest(int Status, Dictionary<string, string>? Headers, string? Body);

    internal sealed record AutoReplyRequest(string? Template);
}

/// <summary>
/// The log category of intercepted requests.
/// </summary>
internal sealed class InterceptLog;
