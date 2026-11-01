using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Logs each call to the LinkMobility SMS gateway in curl -v style when <see cref="SmsGatewaySettings.LogPayloads"/> is
/// <see cref="PayloadLogMode.Plain"/>: the request and response lines and headers, the elapsed time, and each body as text,
/// escaped and as hex, with only the password and authorization headers masked. The bytes that are sent are not changed.
/// </summary>
/// <remarks>
/// Logs at Warning, so the entries pass the Warning filter on OpenTelemetry in Staging. Headers that the socket handler adds
/// below this handler, such as Host and Connection, are not shown.
/// </remarks>
internal sealed class PayloadLoggingHandler(ILogger<PayloadLoggingHandler> logger) : DelegatingHandler
{
    private static readonly HashSet<string> _maskedHeaders = new(StringComparer.OrdinalIgnoreCase) { "Authorization", "Proxy-Authorization" };

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // StringContent is buffered, so reading it here leaves the same bytes to be sent.
        byte[]? requestBody = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        long start = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        byte[] responseBody;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
            responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "LinkMobility payload {Method} {Uri} -> {Outcome} in {ElapsedMs} ms\n{Exchange}",
                request.Method,
                request.RequestUri,
                ex.GetType().Name,
                ElapsedMs(start),
                Describe(request, requestBody, null, null, ex));
            throw;
        }

        long elapsedMs = ElapsedMs(start);
        ReplaceContent(response, responseBody);

        logger.LogWarning(
            "LinkMobility payload {Method} {Uri} -> {Outcome} in {ElapsedMs} ms\n{Exchange}",
            request.Method,
            request.RequestUri,
            (int)response.StatusCode,
            elapsedMs,
            Describe(request, requestBody, response, responseBody, null));

        return response;
    }

    /// <summary>
    /// The request and response as curl -v shows them: <c>&gt;</c> before what was sent, <c>&lt;</c> before what came back.
    /// </summary>
    internal static string Describe(HttpRequestMessage request, byte[]? requestBody, HttpResponseMessage? response, byte[]? responseBody, Exception? exception)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"> {request.Method} {request.RequestUri?.PathAndQuery} HTTP/{request.Version}\n");

        // The socket handler computes Content-Length the same way, so asking for it here does not change what is sent.
        _ = request.Content?.Headers.ContentLength;
        AppendHeaders(text, "> ", request.Headers, request.Content?.Headers);
        text.Append(">\n")
            .Append(requestBody is null ? "Request: no body" : PlainPayload.Describe("Request", requestBody, request.Content?.Headers.ContentType?.ToString()))
            .Append('\n');

        if (response is null)
        {
            return text.Append(CultureInfo.InvariantCulture, $"< no response: {exception?.GetType().FullName}: {exception?.Message}").ToString();
        }

        text.Append(CultureInfo.InvariantCulture, $"< HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}\n");
        AppendHeaders(text, "< ", response.Headers, response.Content.Headers);
        return text.Append("<\n")
            .Append(PlainPayload.Describe("Response", responseBody ?? [], response.Content.Headers.ContentType?.ToString()))
            .ToString();
    }

    /// <summary>
    /// Puts the bytes that were read back as the response content, with the same headers, so the gateway client can still read it.
    /// </summary>
    internal static void ReplaceContent(HttpResponseMessage response, byte[] body)
    {
        var content = new ByteArrayContent(body);
        foreach (var header in response.Content.Headers)
        {
            content.Headers.Remove(header.Key);
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content.Dispose();
        response.Content = content;
    }

    private static void AppendHeaders(StringBuilder text, string prefix, HttpHeaders headers, HttpContentHeaders? contentHeaders)
    {
        foreach (var header in contentHeaders is null ? headers : headers.Concat(contentHeaders))
        {
            string value = _maskedHeaders.Contains(header.Key) ? "***" : string.Join(", ", header.Value);
            text.Append(CultureInfo.InvariantCulture, $"{prefix}{header.Key}: {value}\n");
        }
    }

    private static long ElapsedMs(long start)
    {
        return (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
}
