using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.LinkMobility;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using LinkMobilityModel = LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class PayloadLoggingHandlerTests
{
    private const string Password = "s3cret-pass";

    private const string AcceptedXml = """
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <SESSION>
          <LOGON>OK</LOGON>
          <MSGLST>
            <MSG>
              <ID>1</ID>
              <REF>gateway-ref-123</REF>
              <STATUS>OK</STATUS>
            </MSG>
          </MSGLST>
        </SESSION>
        """;

    [Fact]
    public async Task SendAsync_LogPayloadsOff_GatewayCallIsNotLogged()
    {
        // Arrange
        var gateway = new RecordingHandler(AcceptedResponse);
        var logs = new CapturingLoggerProvider();
        var client = CreateGatewayClient("Off", gateway, logs);

        // Act
        var result = await client.SendAsync(new LinkMobilityModel.Sms("Altinn", "+4799999999", "Hei"));

        // Assert
        Assert.True(result.IsStatusOk);
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("LinkMobility payload"));
    }

    [Fact]
    public async Task SendAsync_LogPayloadsPlain_LogsOneWarningWithPasswordMaskedAndResponseStillParses()
    {
        // Arrange
        var gateway = new RecordingHandler(AcceptedResponse);
        var logs = new CapturingLoggerProvider();
        var client = CreateGatewayClient("Plain", gateway, logs);

        // Act
        var result = await client.SendAsync(new LinkMobilityModel.Sms("Altinn", "+4799999999", "Linje 1\nLinje 2 æøå"));

        // Assert
        Assert.True(result.IsStatusOk);
        Assert.Equal("gateway-ref-123", result.GatewayReference);

        var entry = Assert.Single(logs.Entries, e => e.Message.Contains("LinkMobility payload"));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(typeof(PayloadLoggingHandler).FullName, entry.Category);
        Assert.Contains("<PW>***</PW>", entry.Message);
        Assert.DoesNotContain(Password, entry.Message);
        Assert.DoesNotContain(PlainPayload.Hex(Encoding.UTF8.GetBytes(Password)), entry.Message);
        Assert.Contains("+4799999999", entry.Message);
        Assert.Contains("Linje 1", entry.Message);
        Assert.Contains("> POST ", entry.Message);
        Assert.Contains("> Content-Type: text/xml; charset=utf-8", entry.Message);
        Assert.Contains("< HTTP/1.1 200 OK", entry.Message);
        Assert.Contains("Hex: ", entry.Message);
    }

    [Fact]
    public async Task SendAsync_LogPayloadsPlain_SendsTheSameBytesAsWithoutLogging()
    {
        // Arrange
        var sms = new LinkMobilityModel.Sms("Altinn", "+4799999999", "Linje 1\r\nLinje 2\tæøå €");
        var withoutLogging = new RecordingHandler(AcceptedResponse);
        var withLogging = new RecordingHandler(AcceptedResponse);

        // Act
        await CreateGatewayClient("Off", withoutLogging, new CapturingLoggerProvider()).SendAsync(sms);
        await CreateGatewayClient("Plain", withLogging, new CapturingLoggerProvider()).SendAsync(sms);

        // Assert
        Assert.NotNull(withoutLogging.RequestBody);
        Assert.Equal(withoutLogging.RequestBody, withLogging.RequestBody);
        Assert.Equal(withoutLogging.RequestContentHeaders, withLogging.RequestContentHeaders);
    }

    [Fact]
    public async Task SendAsync_StringContent_InnerHandlerGetsExactBytesAndHeaders()
    {
        // Arrange
        string body = "<SESSION><PW>" + Password + "</PW><TEXT>a\r\nb</TEXT></SESSION>";
        var gateway = new RecordingHandler(AcceptedResponse);
        var logger = new CapturingLogger();
        using var invoker = new HttpMessageInvoker(new PayloadLoggingHandler(logger) { InnerHandler = gateway });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://xml-test.pswin.com/")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        };

        // Act
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Encoding.UTF8.GetBytes(body), gateway.RequestBody);
        Assert.Contains("Content-Type: text/xml; charset=utf-8", gateway.RequestContentHeaders);
    }

    [Fact]
    public async Task SendAsync_ResponseWithControlCharactersAndInvalidBytes_ShowsThemEscapedAndInHex()
    {
        // Arrange
        byte[] responseBody = [.. "<SESSION>a\u0007b c\td\r\n"u8, 0xFF, .. "</SESSION>"u8];
        var gateway = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(responseBody) { Headers = { ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8") } },
        });
        var logger = new CapturingLogger();
        using var invoker = new HttpMessageInvoker(new PayloadLoggingHandler(logger) { InnerHandler = gateway });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://xml-test.pswin.com/") { Content = new StringContent("<SESSION/>") };

        // Act
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Contains(@"Escaped: <SESSION>a\u0007b[U+00A0]c\td\r\n[U+FFFD]</SESSION>", entry.Message);
        Assert.Contains("61 07 62 C2 A0 63 09 64 0D 0A FF 3C", entry.Message);
        Assert.Equal(responseBody, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("text/xml; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task SendAsync_AuthorizationHeader_IsMasked()
    {
        // Arrange
        var gateway = new RecordingHandler(AcceptedResponse);
        var logger = new CapturingLogger();
        using var invoker = new HttpMessageInvoker(new PayloadLoggingHandler(logger) { InnerHandler = gateway });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://xml-test.pswin.com/") { Content = new StringContent("<SESSION/>") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNz");

        // Act
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Contains("> Authorization: ***", entry.Message);
        Assert.DoesNotContain("dXNlcjpwYXNz", entry.Message);
        Assert.Equal("dXNlcjpwYXNz", gateway.Authorization);
    }

    [Fact]
    public async Task SendAsync_PasswordNotFoundInBytes_LeavesOutHex()
    {
        // Arrange
        var gateway = new RecordingHandler(AcceptedResponse);
        var logger = new CapturingLogger();
        using var invoker = new HttpMessageInvoker(new PayloadLoggingHandler(logger) { InnerHandler = gateway });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://xml-test.pswin.com/")
        {
            Content = new StringContent("<SESSION><PW>" + Password + "</PW></SESSION>", Encoding.Unicode, "text/xml"),
        };

        // Act
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Contains("<PW>***</PW>", entry.Message);
        Assert.DoesNotContain(Password, entry.Message);
        Assert.Contains("Hex: [left out: the password could not be masked in the bytes]", entry.Message);
    }

    [Fact]
    public async Task SendAsync_InnerHandlerThrows_LogsRequestAndRethrows()
    {
        // Arrange
        var gateway = new RecordingHandler(() => throw new HttpRequestException("Connection refused"));
        var logger = new CapturingLogger();
        using var invoker = new HttpMessageInvoker(new PayloadLoggingHandler(logger) { InnerHandler = gateway });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://xml-test.pswin.com/") { Content = new StringContent("<SESSION/>") };

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(() => invoker.SendAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Request: text/plain; charset=utf-8, 10 bytes", entry.Message);
        Assert.Contains("< no response: System.Net.Http.HttpRequestException: Connection refused", entry.Message);
    }

    [Fact]
    public async Task StartAsync_PayloadLoggingWarning_LogsWarning()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        await new PayloadLoggingWarning(logger).StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("LogPayloads is Plain", entry.Message);
    }

    private static HttpResponseMessage AcceptedResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(AcceptedXml, Encoding.UTF8, "text/xml") };
    }

    private static IAltinnGatewayClient CreateGatewayClient(string logPayloads, HttpMessageHandler gateway, ILoggerProvider logs)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmsGatewaySettings:Username"] = "test-user",
                ["SmsGatewaySettings:Password"] = Password,
                ["SmsGatewaySettings:Endpoint"] = "https://xml-test.pswin.com",
                ["SmsGatewaySettings:LogPayloads"] = logPayloads,
            })
            .Build();

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Staging");

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs));
        services.AddSmsGatewayServices(config, env.Object);
        services.AddHttpClient<IAltinnGatewayClient, AltinnGatewayClient>().ConfigurePrimaryHttpMessageHandler(() => gateway);

        return services.BuildServiceProvider().GetRequiredService<IAltinnGatewayClient>();
    }

    private sealed class RecordingHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public byte[]? RequestBody { get; private set; }

        public string? RequestContentHeaders { get; private set; }

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Read the body the way the socket handler does: it asks for Content-Length and serializes the content to a stream.
            using var stream = new MemoryStream();
            if (request.Content is not null)
            {
                _ = request.Content.Headers.ContentLength;
                await request.Content.CopyToAsync(stream, cancellationToken);
                RequestContentHeaders = request.Content.Headers.ToString();
            }

            RequestBody = stream.ToArray();
            Authorization = request.Headers.Authorization?.Parameter;
            return respond();
        }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class CapturingLogger(string category = "") : ILogger<PayloadLoggingHandler>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
            {
                Entries.Add(new LogEntry(category, logLevel, formatter(state, exception)));
            }
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<CapturingLogger> _loggers = [];

        public IEnumerable<LogEntry> Entries => _loggers.SelectMany(l => l.Entries);

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new CapturingLogger(categoryName);
            _loggers.Add(logger);
            return logger;
        }

        public void Dispose()
        {
        }
    }
}
