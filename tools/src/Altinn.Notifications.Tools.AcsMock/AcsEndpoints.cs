using System.Text;
using System.Text.Json;

using Altinn.Notifications.Tools.AcsMock.Configuration;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// The two ACS Email REST operations the SDK's <c>EmailClient</c> uses, and a fallback for anything else.
/// </summary>
internal static class AcsEndpoints
{
    internal const string FallbackClientName = "AcsMockFallback";

    public static void MapAcsEndpoints(this WebApplication app)
    {
        app.MapPost("/emails:send", SendAsync);
        app.MapGet("/emails/operations/{operationId}", GetOperationAsync);
        app.Map("{**path}", FallbackAsync);
    }

    private static async Task SendAsync(HttpContext context, EmailOperationStore store, EmailMirror mirror, IOptions<AcsMockSettings> options, TimeProvider timeProvider, ILogger<AcsMockLog> logger)
    {
        var settings = options.Value;
        byte[] body = await ReadBodyAsync(context);
        string requestBody = Encoding.UTF8.GetString(body);
        EmailScenario? scenario = null;

        IResult result;
        if (!IsSigned(context, settings, body, logger))
        {
            result = Error(StatusCodes.Status401Unauthorized, "Denied", "Denied by the resource provider.");
        }
        else if (!TryReadMessage(requestBody, out string sender, out List<string> recipients))
        {
            result = Error(StatusCodes.Status400BadRequest, "BadRequest", "The request body is not a valid email message with at least one recipient.");
        }
        else
        {
            scenario = EmailScenarios.Pick(recipients);
            result = scenario switch
            {
                EmailScenario.InvalidAddress => Error(StatusCodes.Status400BadRequest, "BadRequest", "Invalid format for email address."),
                EmailScenario.Throttled when store.IsFirstAttempt(HmacValidator.ContentHash(body)) => Throttled(context, settings.ThrottleRetryAfterSeconds),
                _ => Accept(context, store, timeProvider, settings, scenario.Value, sender, recipients),
            };

            // A throttled first attempt is left out, since the email service sends it again.
            if (scenario == EmailScenario.InvalidAddress)
            {
                mirror.Received(requestBody, null, scenario.Value, "Invalid format for email address.");
            }
            else if (context.Response.Headers.TryGetValue("operation-id", out var operationId))
            {
                mirror.Received(requestBody, operationId.ToString(), scenario.Value);
            }
        }

        await LogAndExecuteAsync(context, result, requestBody, scenario, logger);
    }

    private static async Task GetOperationAsync(HttpContext context, string operationId, EmailOperationStore store, DeliveryReportService deliveryReports, EmailMirror mirror, IOptions<AcsMockSettings> options, TimeProvider timeProvider, ILogger<AcsMockLog> logger)
    {
        var operation = store.Get(operationId);

        IResult result;
        if (!IsSigned(context, options.Value, [], logger))
        {
            result = Error(StatusCodes.Status401Unauthorized, "Denied", "Denied by the resource provider.");
        }
        else if (operation is null)
        {
            result = Error(StatusCodes.Status404NotFound, "NotFound", $"Operation {operationId} was not found. The mock keeps operations in memory, so they are lost when it restarts.");
        }
        else
        {
            string status = operation.Status(timeProvider.GetUtcNow());
            if (status == "Succeeded")
            {
                deliveryReports.Queue(operation);
            }
            else if (status == "Failed" && operation.MarkFailureMirrored())
            {
                mirror.Completed(operation.Id, operation.Scenario == EmailScenario.Suppressed ? "EmailDroppedAllRecipientsSuppressed" : "EmailSendFailed");
            }

            result = Results.Json(new { id = operation.Id, status, error = OperationError(operation, status) });
        }

        await LogAndExecuteAsync(context, result, string.Empty, operation?.Scenario, logger);
    }

    /// <summary>
    /// Forwards anything the mock does not handle to <c>{FallbackUrl}acs/{path}</c>, so an operator can answer it in the simulator.
    /// </summary>
    private static async Task FallbackAsync(HttpContext context, IOptions<AcsMockSettings> options, IHttpClientFactory httpClientFactory, ILogger<AcsMockLog> logger)
    {
        string fallbackUrl = options.Value.FallbackUrl;
        byte[] body = await ReadBodyAsync(context);
        string requestBody = Encoding.UTF8.GetString(body);

        if (string.IsNullOrWhiteSpace(fallbackUrl))
        {
            await LogAndExecuteAsync(context, Results.NotFound(), requestBody, null, logger);
            return;
        }

        var target = new Uri(fallbackUrl.TrimEnd('/') + "/acs" + RawTarget(context));
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
        if (body.Length > 0)
        {
            request.Content = new ByteArrayContent(body);
        }

        foreach (var (name, values) in context.Request.Headers)
        {
            if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(name, values.ToArray()))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, values.ToArray());
            }
        }

        using var client = httpClientFactory.CreateClient(FallbackClientName);
        using var response = await client.SendAsync(request, context.RequestAborted);
        byte[] responseBody = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
        {
            if (!name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers[name] = values.ToArray();
            }
        }

        await context.Response.Body.WriteAsync(responseBody, context.RequestAborted);

        logger.LogInformation(
            "ACS fallback {Method} {Path} -> {Target} -> {StatusCode}\nRequest: {RequestBody}\nResponse: {ResponseBody}",
            context.Request.Method,
            RawTarget(context),
            target,
            context.Response.StatusCode,
            PayloadMasker.Mask(requestBody),
            PayloadMasker.Mask(Encoding.UTF8.GetString(responseBody)));
    }

    private static IResult Accept(HttpContext context, EmailOperationStore store, TimeProvider timeProvider, AcsMockSettings settings, EmailScenario scenario, string sender, List<string> recipients)
    {
        // The SDK sends its own operation id. Real ACS uses it, and returns the same operation for a retried send.
        string id = context.Request.Headers["Operation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        int runningSeconds = scenario == EmailScenario.Slow ? settings.SlowSeconds : settings.RunningSeconds;

        var operation = store.GetOrAdd(new EmailOperation
        {
            Id = id,
            Scenario = scenario,
            Sender = sender,
            Recipients = recipients,
            CompletesAt = timeProvider.GetUtcNow().AddSeconds(runningSeconds),
            TraceParent = context.Request.Headers["traceparent"].FirstOrDefault(),
            TraceState = context.Request.Headers["tracestate"].FirstOrDefault(),
        });

        string apiVersion = context.Request.Query["api-version"].FirstOrDefault() ?? string.Empty;
        context.Response.Headers["Operation-Location"] = $"{context.Request.Scheme}://{context.Request.Host}/emails/operations/{operation.Id}?api-version={apiVersion}";
        context.Response.Headers["operation-id"] = operation.Id;
        return Results.Json(new { id = operation.Id, status = "Running", error = (object?)null }, statusCode: StatusCodes.Status202Accepted);
    }

    private static IResult Throttled(HttpContext context, int retryAfterSeconds)
    {
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        return Error(StatusCodes.Status429TooManyRequests, "TooManyRequests", $"Simulated throttling by the ACS mock. Please try again after {retryAfterSeconds} seconds.");
    }

    private static object? OperationError(EmailOperation operation, string status)
    {
        if (status != "Failed")
        {
            return null;
        }

        return operation.Scenario == EmailScenario.Suppressed
            ? new { code = "EmailDroppedAllRecipientsSuppressed", message = "Message was dropped because all recipients were suppressed." }
            : new { code = "EmailSendFailed", message = "Simulated failure by the ACS mock." };
    }

    private static IResult Error(int statusCode, string code, string message)
    {
        return Results.Json(new { error = new { code, message } }, statusCode: statusCode);
    }

    private static bool IsSigned(HttpContext context, AcsMockSettings settings, byte[] body, ILogger logger)
    {
        var headers = context.Request.Headers;
        bool valid = HmacValidator.IsValid(
            settings.AccessKey,
            context.Request.Method,
            RawTarget(context),
            context.Request.Host.Value ?? string.Empty,
            headers["x-ms-date"].FirstOrDefault(),
            headers["x-ms-content-sha256"].FirstOrDefault(),
            headers.Authorization.FirstOrDefault(),
            body,
            out string reason);

        if (!valid)
        {
            logger.LogWarning("ACS {Method} {Path} rejected with 401: {Reason}. Check that the accesskey in the connection string matches AcsMockSettings:AccessKey.", context.Request.Method, RawTarget(context), reason);
        }

        return valid;
    }

    private static bool TryReadMessage(string json, out string sender, out List<string> recipients)
    {
        sender = string.Empty;
        recipients = [];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            sender = root.TryGetProperty("senderAddress", out var senderAddress) ? senderAddress.GetString() ?? string.Empty : string.Empty;

            if (root.TryGetProperty("recipients", out var all))
            {
                foreach (string kind in new[] { "to", "cc", "bcc" })
                {
                    if (all.TryGetProperty(kind, out var list) && list.ValueKind == JsonValueKind.Array)
                    {
                        recipients.AddRange(list.EnumerateArray()
                            .Select(r => r.TryGetProperty("address", out var a) ? a.GetString() : null)
                            .OfType<string>());
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return recipients.Count > 0;
    }

    private static async Task LogAndExecuteAsync(HttpContext context, IResult result, string requestBody, EmailScenario? scenario, ILogger logger)
    {
        // Capture the response body, which only holds ids, statuses and error messages, so it can be logged as sent.
        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await result.ExecuteAsync(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        string responseBody = Encoding.UTF8.GetString(buffer.ToArray());
        await buffer.CopyToAsync(original, context.RequestAborted);

        logger.LogInformation(
            "ACS {Method} {Path} -> {StatusCode} ({Scenario})\nRequest: {RequestBody}\nResponse: {ResponseBody}",
            context.Request.Method,
            RawTarget(context),
            context.Response.StatusCode,
            scenario?.ToString() ?? "-",
            PayloadMasker.Mask(requestBody),
            responseBody);
    }

    private static async Task<byte[]> ReadBodyAsync(HttpContext context)
    {
        using var stream = new MemoryStream();
        await context.Request.Body.CopyToAsync(stream, context.RequestAborted);
        return stream.ToArray();
    }

    /// <summary>
    /// The path and query exactly as the client sent them, since the SDK signs those and not the decoded path.
    /// </summary>
    private static string RawTarget(HttpContext context)
    {
        string? raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        return string.IsNullOrEmpty(raw) ? context.Request.Path + context.Request.QueryString : raw;
    }
}

/// <summary>
/// The log category of the mock's request log, so it can be filtered on in the dashboard.
/// </summary>
internal sealed class AcsMockLog;
