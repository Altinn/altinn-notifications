using System.Net.Http;
using System.Threading.Tasks;

using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.Integrations.LinkMobility;
using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

using SendingSms = Altinn.Notifications.Sms.Core.Sending.Sms;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

/// <summary>
/// Sends through the SMS service's own <see cref="SmsClient"/> and LinkMobility's gateway client, and feeds the delivery report
/// back through its receiver and mapper, so each scenario ends with the result the SMS service would publish.
/// </summary>
public class SmsClientTests
{
    [Theory]
    [InlineData("+4799999999", SmsSendResult.Delivered)]
    [InlineData("+4799999901", SmsSendResult.Failed_Undelivered)]
    [InlineData("+4799999902", SmsSendResult.Failed_Expired)]
    [InlineData("+4799999905", SmsSendResult.Failed_Rejected)]
    [InlineData("+4799999906", SmsSendResult.Failed_BarredReceiver)]
    public async Task Send_AcceptedScenario_GetsReferenceThenMatchingDeliveryReport(string recipient, SmsSendResult expected)
    {
        await using var host = await ProxyHost.StartAsync();
        using var http = new HttpClient();

        var sent = await CreateClient(http, host.Url).SendAsync(NewSms(recipient));
        string reference = sent.Match(r => r, e => throw new InvalidOperationException(e.SendResult.ToString()));

        var report = await ProxyHost.ReceiveAsync(await host.Sender.WaitForReportAsync());
        Assert.Equal(reference, report.Reference);
        Assert.Equal(expected, SmsSendResultMapper.ParseDeliveryState(report.State));
    }

    [Fact]
    public async Task Send_InvalidRecipientScenario_FailsWithoutReport()
    {
        await using var host = await ProxyHost.StartAsync();
        using var http = new HttpClient();

        var sent = await CreateClient(http, host.Url).SendAsync(NewSms("+4799999904"));

        Assert.Equal(SmsSendResult.Failed_InvalidRecipient, sent.Match(_ => SmsSendResult.Accepted, e => e.SendResult));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(host.Sender.Sent);
    }

    [Fact]
    public async Task Send_NoReportScenario_GetsReferenceButNoReport()
    {
        await using var host = await ProxyHost.StartAsync();
        using var http = new HttpClient();

        var sent = await CreateClient(http, host.Url).SendAsync(NewSms("+4799999903"));

        Assert.True(sent.IsSuccess);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(host.Sender.Sent);
    }

    [Fact]
    public async Task Send_Twice_GetsDifferentReferences()
    {
        await using var host = await ProxyHost.StartAsync();
        using var http = new HttpClient();
        var client = CreateClient(http, host.Url);

        var first = await client.SendAsync(NewSms("+4799999999"));
        var second = await client.SendAsync(NewSms("+4799999999"));

        Assert.NotEqual(first.Match(r => r, _ => "first"), second.Match(r => r, _ => "second"));
    }

    [Fact]
    public async Task Send_Plain_LogsTheBytesTheGatewayClientSends()
    {
        await using var host = await ProxyHost.StartAsync(s => s.LogPayloads = PayloadLogMode.Plain);
        using var http = new HttpClient();

        await CreateClient(http, host.Url).SendAsync(new SendingSms(Guid.NewGuid(), "Altinn", "+4799999999", "Hei\næøå\u00A0x"));

        // The gateway client posts UTF-8 bytes under an XML declaration that says iso-8859-1, and writes line breaks as the
        // machine's own, so a line feed is sent as CR LF from Windows and a CR LF as a line feed from Linux.
        var entry = await host.Logger.WaitForAsync("LinkMobility stub POST / -> 200 (Delivered)");
        string newLine = Environment.NewLine.Replace("\r", @"\r").Replace("\n", @"\n");
        Assert.Contains("Request: text/xml; charset=utf-8, ", entry);
        Assert.Contains($@"Escaped: <?xml version=""1.0"" encoding=""iso-8859-1""?><SESSION>{newLine}  <CLIENT>user</CLIENT>{newLine}  <PW>***</PW>", entry);
        Assert.Contains($@"<TEXT>Hei{newLine}æøå[U+00A0]x</TEXT>", entry);
        Assert.Contains("C3 A6 C3 B8 C3 A5 C2 A0 78 3C 2F 54 45 58 54 3E", entry);
        Assert.DoesNotContain("secret", entry);
    }

    [Fact]
    public async Task Send_ControlCharacterXmlDoesNotAllow_FailsInTheGatewayClientBeforeAnythingIsSent()
    {
        await using var host = await ProxyHost.StartAsync(s => s.LogPayloads = PayloadLogMode.Plain);
        using var http = new HttpClient();

        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(http, host.Url).SendAsync(new SendingSms(Guid.NewGuid(), "Altinn", "+4799999999", "a\u0007b")));

        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(host.Logger.Messages, m => m.StartsWith("LinkMobility stub POST", StringComparison.Ordinal));
    }

    private static SmsClient CreateClient(HttpClient http, string url)
    {
        var settings = new SmsGatewaySettings { Username = "user", Password = "secret", Endpoint = url };
        return new SmsClient(new AltinnGatewayClient(http, settings), NullLogger<SmsClient>.Instance);
    }

    private static SendingSms NewSms(string recipient)
    {
        return new SendingSms(Guid.NewGuid(), "Altinn", recipient, "Kode: 123456");
    }
}
