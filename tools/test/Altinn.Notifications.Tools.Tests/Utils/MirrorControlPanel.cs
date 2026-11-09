using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.Utils;

/// <summary>
/// A WireMock server in place of the test harness control panel's <c>/mirror/</c>, answering every POST with 201.
/// </summary>
public static class MirrorControlPanel
{
    /// <summary>
    /// Starts the server and waits until it has answered once, then clears its log.
    /// The first request to a new WireMock server can take several seconds on a cold start (17 s right after a build),
    /// longer than the mirrors' 2-second timeout, and a mirror drops a copy that times out.
    /// </summary>
    public static async Task<WireMockServer> StartAsync()
    {
        var server = WireMockServer.Start();
        try
        {
            server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(201));

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            using var response = await http.PostAsync(server.Url + "/warmup", null, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            server.ResetLogEntries();
            return server;
        }
        catch
        {
            server.Dispose();
            throw;
        }
    }
}
