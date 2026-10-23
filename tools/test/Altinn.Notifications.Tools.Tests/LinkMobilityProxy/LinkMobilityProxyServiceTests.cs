using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.LinkMobilityProxy;
using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

public class LinkMobilityProxyServiceTests
{
    private const string SendRequest = """<?xml version="1.0" encoding="iso-8859-1"?><SESSION><CLIENT>user</CLIENT><PW>secret</PW><MSGLST><MSG><ID>1</ID><TEXT>Hei æøå 123456</TEXT><SND>Altinn</SND><RCV>+4799123456</RCV></MSG></MSGLST></SESSION>""";

    [Fact]
    public async Task StubMode_AnswersOkAndLogsMaskedPayload()
    {
        var logger = new ListLogger();
        var service = new LinkMobilityProxyService(Options.Create(new LinkMobilityProxySettings { Port = FreePort() }), logger);
        await service.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var http = new HttpClient();
            using var response = await http.PostAsync(service.Url, new StringContent(SendRequest, Encoding.UTF8, "text/xml"), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(LinkMobilityProxyService.StubResponse, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            var entry = await logger.WaitForAsync("LinkMobility stub POST");
            Assert.Contains("<PW>***</PW>", entry);
            Assert.Contains("<RCV>+********56</RCV>", entry);
            Assert.DoesNotContain("secret", entry);
            Assert.DoesNotContain("123456", entry);
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ProxyMode_ForwardsUnmaskedPayloadButLogsMaskedPayload()
    {
        using var gateway = WireMockServer.Start();
        gateway.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(200).WithBody("<SESSION><LOGON>OK</LOGON></SESSION>"));

        var logger = new ListLogger();
        var settings = new LinkMobilityProxySettings { Port = FreePort(), Target = gateway.Url! };
        var service = new LinkMobilityProxyService(Options.Create(settings), logger);
        await service.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var http = new HttpClient();
            using var response = await http.PostAsync(service.Url, new StringContent(SendRequest, Encoding.UTF8, "text/xml"), TestContext.Current.CancellationToken);

            Assert.Equal("<SESSION><LOGON>OK</LOGON></SESSION>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(SendRequest, Assert.Single(gateway.LogEntries).RequestMessage!.Body);

            var entry = await logger.WaitForAsync("LinkMobility proxy POST");
            Assert.Contains("<PW>***</PW>", entry);
            Assert.DoesNotContain("secret", entry);
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class ListLogger : ILogger<LinkMobilityProxyService>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
        }

        // WireMock raises LogEntriesChanged after the response is written, so the entry can arrive a little later.
        public async Task<string> WaitForAsync(string prefix)
        {
            for (int i = 0; i < 50; i++)
            {
                var match = _messages.FirstOrDefault(m => m.StartsWith(prefix, StringComparison.Ordinal));
                if (match is not null)
                {
                    return match;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException($"No log entry starting with '{prefix}'. Got: {string.Join(" | ", _messages)}");
        }
    }
}
