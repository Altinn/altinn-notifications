using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Altinn.Notifications.Email.Integrations.Clients.AzureCommunicationServices;

/// <summary>
/// Policy that converts HTTP 429 responses into a <see cref="RequestFailedException"/> with
/// ErrorCode = <c>ErrorTypes.ExcessiveCallVolumeErrorCode</c>. This allows higher layers to
/// classify the failure as a transient excessive call volume error.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TooManyRequestsPolicy : HttpPipelineSynchronousPolicy
{
    private const int _tooManyRequestsStatusCode = 429;
    private const string _retryAfterHeader = "Retry-After";

    /// <summary>
    /// Method is invoked after the response is received.
    /// </summary>
    /// <param name="message">The <see cref="T:Azure.Core.HttpMessage" /> containing the response.</param>
    public override void OnReceivedResponse(HttpMessage message)
    {
        if (message.Response.Status != _tooManyRequestsStatusCode)
        {
            base.OnReceivedResponse(message);
            return;
        }

        int? retrySeconds = TryGetRetryAfterSeconds(message.Response);

        string reason = message.Response.ReasonPhrase ?? "Too Many Requests";
        string composedMessage = retrySeconds is > 0
            ? $"HTTP 429 (Too Many Requests). Retry after {retrySeconds} seconds. {reason}"
            : $"HTTP 429 (Too Many Requests). {reason}";

        var requestFailedException = new RequestFailedException(
            innerException: null,
            message: composedMessage,
            status: message.Response.Status,
            errorCode: ErrorTypes.ExcessiveCallVolumeErrorCode);

        if (retrySeconds.HasValue)
        {
            requestFailedException.Data["RetryAfterSeconds"] = retrySeconds.Value;
        }

        throw requestFailedException;
    }

    /// <summary>
    /// Attempts to extract a retry delay (in seconds) from the Retry-After header.
    /// Supports either an integer number of seconds or an HTTP-date per RFC 7231.
    /// </summary>
    private static int? TryGetRetryAfterSeconds(Response response)
    {
        if (!response.Headers.TryGetValue(_retryAfterHeader, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Case 1: delta-seconds (integer)
        if (int.TryParse(value, out int seconds) && seconds >= 0)
        {
            return seconds;
        }

        // Case 2: HTTP-date
        // Retry-After date indicates when the request can be retried; compute delta in whole seconds (minimum 1).
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var retryTime))
        {
            var delta = retryTime - DateTimeOffset.UtcNow;
            if (delta.TotalSeconds > 0)
            {
                return (int)Math.Ceiling(delta.TotalSeconds);
            }
        }

        return null;
    }
}
