using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.LinkMobilityProxy;
using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using LinkMobility.GatewayReceiver;
using LinkMobility.PSWin.Receiver.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

/// <summary>
/// Runs the proxy on a free port with a <see cref="CapturingSender"/> in place of the SMS service's delivery report endpoint.
/// </summary>
internal sealed class ProxyHost : IAsyncDisposable
{
    private ProxyHost(LinkMobilityProxySettings settings)
    {
        Settings = settings;
        Mirror = new SmsMirror(Options.Create(settings), new ListLogger<SmsMirror>());
        Reports = new DeliveryReportService(Sender, Mirror, Options.Create(settings), TimeProvider.System, new ListLogger<DeliveryReportService>());
        Service = new LinkMobilityProxyService(Options.Create(settings), Reports, Mirror, Logger);
    }

    public LinkMobilityProxySettings Settings { get; }

    public CapturingSender Sender { get; } = new();

    public ListLogger<LinkMobilityProxyService> Logger { get; } = new();

    public DeliveryReportService Reports { get; }

    public SmsMirror Mirror { get; }

    public LinkMobilityProxyService Service { get; }

    public string Url => Service.Url!;

    /// <summary>
    /// Starts the proxy. Delivery reports are on and sent without delay, unless <paramref name="configure"/> changes that.
    /// </summary>
    public static async Task<ProxyHost> StartAsync(Action<LinkMobilityProxySettings>? configure = null)
    {
        var settings = new LinkMobilityProxySettings
        {
            Port = FreePort(),
            DeliveryReportUrl = "http://localhost/notifications/sms/api/v1/reports",
            DeliveryReportDelaySeconds = 0,
        };
        configure?.Invoke(settings);

        var host = new ProxyHost(settings);
        await host.Mirror.StartAsync(CancellationToken.None);
        await host.Reports.StartAsync(CancellationToken.None);
        await host.Service.StartAsync(CancellationToken.None);
        return host;
    }

    /// <summary>
    /// The XML the SMS service's gateway client posts for one message.
    /// </summary>
    public static string SendRequest(string recipient, string id = "1")
    {
        return $"""<?xml version="1.0" encoding="iso-8859-1"?><SESSION><CLIENT>user</CLIENT><PW>secret</PW><MSGLST><MSG><ID>{id}</ID><TEXT>Hei æøå 123456</TEXT><SND>Altinn</SND><RCV>{recipient}</RCV><TTL>2880</TTL></MSG></MSGLST></SESSION>""";
    }

    /// <summary>
    /// Feeds a delivery report to the SMS service's own receiver, as its <c>DeliveryReportController</c> does.
    /// </summary>
    public static async Task<DrMessage> ReceiveAsync(string report)
    {
        DrMessage? received = null;
        var receiver = new GatewayReceiver(null, message =>
        {
            received = message;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/xml";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(report));
        context.Response.Body = new MemoryStream();

        await receiver.ReceiveDeliveryReportAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        return received ?? throw new InvalidOperationException("The receiver did not accept the report.");
    }

    public async ValueTask DisposeAsync()
    {
        await Service.StopAsync(CancellationToken.None);
        await Reports.StopAsync(CancellationToken.None);
        Reports.Dispose();
        await Mirror.StopAsync(CancellationToken.None);
        Mirror.Dispose();
    }

    internal static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

/// <summary>
/// Keeps the delivery reports instead of posting them, and answers with the next status code in <see cref="StatusCodes"/>, or 200.
/// </summary>
internal sealed class CapturingSender : IDeliveryReportSender
{
    public ConcurrentQueue<(string Report, string? TraceParent)> Sent { get; } = new();

    public ConcurrentQueue<int> StatusCodes { get; } = new();

    public Task<int> SendAsync(string report, string? traceParent, string? traceState, CancellationToken cancellationToken)
    {
        Sent.Enqueue((report, traceParent));
        return Task.FromResult(StatusCodes.TryDequeue(out int statusCode) ? statusCode : 200);
    }

    public async Task<string> WaitForReportAsync(int count = 1)
    {
        for (int i = 0; i < 50 && Sent.Count < count; i++)
        {
            await Task.Delay(100);
        }

        return Sent.Count >= count ? Sent.ToArray()[count - 1].Report : throw new TimeoutException($"Expected {count} delivery reports, got {Sent.Count}.");
    }
}

internal sealed class ListLogger<T> : ILogger<T>
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Enqueue(formatter(state, exception));
    }

    // WireMock raises LogEntriesChanged after the response is written, so the entry can arrive a little later.
    public async Task<string> WaitForAsync(string prefix)
    {
        for (int i = 0; i < 50; i++)
        {
            var match = Messages.FirstOrDefault(m => m.StartsWith(prefix, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No log entry starting with '{prefix}'. Got: {string.Join(" | ", Messages)}");
    }
}
