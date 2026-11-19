namespace Altinn.Notifications.Aspire.Hosting;

/// <summary>
/// URLs for the mocks, which all listen on localhost on a fixed port.
/// </summary>
internal static class Localhost
{
    /// <summary>
    /// <c>http://localhost:{port}</c>, without a trailing slash.
    /// </summary>
    public static string Url(int port) => new UriBuilder(Uri.UriSchemeHttp, "localhost", port).Uri.GetLeftPart(UriPartial.Authority);
}
