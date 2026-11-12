using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.AcsMock;
using Altinn.Notifications.Tools.AcsMock.Configuration;

using Azure.Communication.Email;
using Azure.Core.Pipeline;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Altinn.Notifications.Tools.Tests.AcsMock;

/// <summary>
/// Runs the mock in memory with operations that complete at once and delivery reports captured instead of sent to Service Bus.
/// </summary>
public class AcsMockFactory : WebApplicationFactory<AcsMockSettings>
{
    public const string AccessKey = "YWx0aW5uLW5vdGlmaWNhdGlvbnMtYWNzLW1vY2stdGVzdC1rZXk=";

    public CapturingDeliveryReportSender DeliveryReports { get; } = new();

    /// <summary>
    /// An SDK client that talks to the in-memory server, signed with <paramref name="accessKey"/>.
    /// Retries are off, so a 429 or 401 reaches the test as it was sent.
    /// </summary>
    public EmailClient CreateEmailClient(string accessKey = AccessKey)
    {
        var options = new EmailClientOptions { Transport = new HttpClientTransport(Server.CreateHandler()) };
        options.Retry.MaxRetries = 0;
        return new EmailClient($"endpoint=http://localhost/;accesskey={accessKey}", options);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("AcsMockSettings:AccessKey", AccessKey);
        builder.UseSetting("AcsMockSettings:RunningSeconds", "0");
        builder.UseSetting("AcsMockSettings:DeliveryReportDelaySeconds", "0");
        builder.UseSetting("AcsMockSettings:ServiceBusConnectionString", "Endpoint=sb://unused.localhost;SharedAccessKeyName=test;SharedAccessKey=test");
        builder.UseSetting("AcsMockSettings:FallbackUrl", string.Empty);
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IDeliveryReportSender>(DeliveryReports)));
    }
}

public sealed class CapturingDeliveryReportSender : IDeliveryReportSender
{
    public ConcurrentQueue<(string Event, string? TraceParent)> Sent { get; } = new();

    Task IDeliveryReportSender.SendAsync(string eventGridEvent, string? traceParent, string? traceState, CancellationToken cancellationToken)
    {
        Sent.Enqueue((eventGridEvent, traceParent));
        return Task.CompletedTask;
    }
}
