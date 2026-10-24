using System.Security.Cryptography;
using System.Text;

namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// Checks the HMAC-SHA256 signature the Azure Communication Services SDK puts on every request.
/// </summary>
internal static class HmacValidator
{
    private const string SignedHeaders = "x-ms-date;host;x-ms-content-sha256";

    /// <summary>
    /// Signs a request the way the SDK's <c>HMACAuthenticationPolicy</c> does.
    /// </summary>
    /// <param name="accessKey">The base64 access key from the connection string.</param>
    /// <param name="method">The HTTP method, e.g. <c>POST</c>.</param>
    /// <param name="pathAndQuery">The path and query as sent, e.g. <c>/emails:send?api-version=2025-09-01</c>.</param>
    /// <param name="host">The host and port, e.g. <c>localhost:5095</c>.</param>
    /// <param name="date">The <c>x-ms-date</c> header.</param>
    /// <param name="contentHash">The <c>x-ms-content-sha256</c> header.</param>
    /// <returns>The value of the <c>Authorization</c> header.</returns>
    public static string Sign(string accessKey, string method, string pathAndQuery, string host, string date, string contentHash)
    {
        string stringToSign = $"{method}\n{pathAndQuery}\n{date};{host};{contentHash}";
        using var hmac = new HMACSHA256(Convert.FromBase64String(accessKey));
        string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(stringToSign)));
        return $"HMAC-SHA256 SignedHeaders={SignedHeaders}&Signature={signature}";
    }

    /// <summary>
    /// The base64 SHA-256 of a request body, as in <c>x-ms-content-sha256</c>.
    /// </summary>
    public static string ContentHash(byte[] body)
    {
        return Convert.ToBase64String(SHA256.HashData(body));
    }

    /// <summary>
    /// Checks the content hash and the signature of a request.
    /// </summary>
    /// <param name="accessKey">The base64 access key the request should be signed with.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The raw path and query of the request.</param>
    /// <param name="host">The <c>Host</c> header.</param>
    /// <param name="date">The <c>x-ms-date</c> header.</param>
    /// <param name="contentHash">The <c>x-ms-content-sha256</c> header.</param>
    /// <param name="authorization">The <c>Authorization</c> header.</param>
    /// <param name="body">The request body.</param>
    /// <param name="reason">Why the request was rejected, when it was.</param>
    /// <returns><c>true</c> when the request was signed with <paramref name="accessKey"/>.</returns>
    public static bool IsValid(string accessKey, string method, string pathAndQuery, string host, string? date, string? contentHash, string? authorization, byte[] body, out string reason)
    {
        if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(contentHash) || string.IsNullOrEmpty(authorization))
        {
            reason = "x-ms-date, x-ms-content-sha256 or Authorization is missing";
            return false;
        }

        if (contentHash != ContentHash(body))
        {
            reason = "x-ms-content-sha256 does not match the body";
            return false;
        }

        string expected = Sign(accessKey, method, pathAndQuery, host, date, contentHash);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(authorization)))
        {
            reason = "the signature does not match the access key";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
