using WireMock;
using WireMock.ResponseBuilders;
using WireMock.Types;
using WireMock.Util;

namespace Altinn.Notifications.Tools.Shared;

/// <summary>
/// Forwards a request a WireMock-based mock does not handle to the test harness control panel, and returns its answer.
/// Linked into each mock project that uses it, since the tools have no shared library.
/// </summary>
internal static class WireMockForwarder
{
    private static readonly string[] _skippedRequestHeaders = ["Host", "Content-Length", "Transfer-Encoding", "Connection"];

    /// <summary>
    /// Sends <paramref name="request"/> to <paramref name="url"/> with its method, headers and body, and returns the response as it came.
    /// </summary>
    /// <param name="client">The client to send with. Its timeout decides how long an operator has to answer.</param>
    /// <param name="url">The full URL, including path and query.</param>
    /// <param name="request">The request the mock received.</param>
    /// <param name="failed">The response when the control panel cannot be reached, given the error message.</param>
    public static async Task<IResponseMessage> ForwardAsync(HttpClient client, string url, IRequestMessage request, Func<string, IResponseMessage> failed)
    {
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
            using var response = await client.SendAsync(forward);
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
            return failed($"Fallback to {url} failed: {ex.Message}");
        }
    }
}
