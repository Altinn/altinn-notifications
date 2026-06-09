using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Email.Integrations.Clients.AzureCommunicationServices;

using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

public class TooManyRequestsPolicyTests
{
    [Fact]
    public void OnReceivedResponse_Non429Response_DoesNotThrowException()
    {
        // Arrange
        var policy = new TooManyRequestsPolicy();
        var mockResponse = new TestResponse(200, "OK", null);
        var message = CreateHttpMessage(mockResponse);

        // Act
        var exception = Record.Exception(() => policy.OnReceivedResponse(message));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void OnReceivedResponse_429WithRetryAfterSeconds_ThrowsRequestFailedExceptionWithCorrectErrorCode()
    {
        // Arrange
        var policy = new TooManyRequestsPolicy();
        var mockResponse = new TestResponse(429, "Too Many Requests", ("Retry-After", "60"));
        var message = CreateHttpMessage(mockResponse);

        // Act & Assert
        var exception = Assert.Throws<RequestFailedException>(() => policy.OnReceivedResponse(message));

        Assert.Equal(ErrorTypes.ExcessiveCallVolumeErrorCode, exception.ErrorCode);
        Assert.Equal(429, exception.Status);
        Assert.Contains("HTTP 429 (Too Many Requests)", exception.Message);
        Assert.Contains("Retry after 60 seconds", exception.Message);
        Assert.Equal(60, exception.Data["RetryAfterSeconds"]);
    }

    [Fact]
    public void OnReceivedResponse_429WithRetryAfterHttpDate_ThrowsRequestFailedExceptionWithCorrectDelay()
    {
        // Arrange
        var policy = new TooManyRequestsPolicy();
        var futureTime = DateTimeOffset.UtcNow.AddSeconds(120);
        var mockResponse = new TestResponse(429, "Too Many Requests", ("Retry-After", futureTime.ToString("R")));
        var message = CreateHttpMessage(mockResponse);

        // Act & Assert
        var exception = Assert.Throws<RequestFailedException>(() => policy.OnReceivedResponse(message));

        Assert.Equal(ErrorTypes.ExcessiveCallVolumeErrorCode, exception.ErrorCode);
        Assert.Equal(429, exception.Status);
        Assert.True(exception.Data.Contains("RetryAfterSeconds"));

        // Should be close to 120 seconds (allowing for processing time)
        var retrySeconds = (int)exception.Data["RetryAfterSeconds"]!;
        Assert.InRange(retrySeconds, 118, 122);
    }

    [Fact]
    public void OnReceivedResponse_429WithInvalidRetryAfterValue_ThrowsRequestFailedExceptionWithoutRetryData()
    {
        // Arrange
        var policy = new TooManyRequestsPolicy();
        var mockResponse = new TestResponse(429, "Too Many Requests", ("Retry-After", "invalid-value"));
        var message = CreateHttpMessage(mockResponse);

        // Act & Assert
        var exception = Assert.Throws<RequestFailedException>(() => policy.OnReceivedResponse(message));

        Assert.False(exception.Data.Contains("RetryAfterSeconds"));
    }

    private static HttpMessage CreateHttpMessage(Response response)
    {
        var request = new HttpClientTransport().CreateRequest();
        var message = new HttpMessage(request, new ResponseClassifier())
        {
            Response = response
        };
        return message;
    }
}

/// <summary>
/// Represents a test implementation of the <see cref="Response"/> class, used for mocking HTTP responses in tests.
/// </summary>
/// <remarks>This class provides a minimal implementation of the <see cref="Response"/> abstract class, allowing
/// tests to simulate HTTP responses with a status code, reason phrase, and an optional single header. It is not
/// intended for production use.</remarks>
/// <param name="statusCode">The http status code to mock</param>
/// <param name="reasonPhrase">The reason phrase associated with the status code</param>
/// <param name="header">Any header that needs to be mocked in the response mock object</param>
internal class TestResponse(int statusCode, string reasonPhrase, (string Key, string Value)? header) : Response
{
    private readonly int _statusCode = statusCode;
    private readonly string _reasonPhrase = reasonPhrase;
    private readonly (string Key, string Value)? _header = header;

    public override int Status => _statusCode;

    public override string ReasonPhrase => _reasonPhrase;

    public override Stream? ContentStream { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override string ClientRequestId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override void Dispose()
    {
        // No unmanaged resources to release in this test type, so make it a no-op.
    }

    protected override bool ContainsHeader(string name)
    {
        if (!_header.HasValue)
        {
            return false;
        }

        return string.Equals(name, _header.Value.Key, StringComparison.OrdinalIgnoreCase);
    }

    protected override IEnumerable<HttpHeader> EnumerateHeaders()
    {
        if (_header.HasValue)
        {
            yield return new HttpHeader(_header.Value.Key, _header.Value.Value);
        }
    }

    protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
    {
        if (ContainsHeader(name))
        {
            value = _header!.Value.Value;
            return true;
        }

        value = null;
        return false;
    }

    // - If the requested header name matches the stored header (case-insensitive), return true and a single-value enumerable.
    // - Otherwise, return false and set values to null.
    protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        if (ContainsHeader(name))
        {
            values = new[] { _header!.Value.Value };
            return true;
        }

        values = null;
        return false;
    }
}
