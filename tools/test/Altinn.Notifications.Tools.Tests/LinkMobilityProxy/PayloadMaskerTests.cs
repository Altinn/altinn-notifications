using Altinn.Notifications.Tools.LinkMobilityProxy;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

public class PayloadMaskerTests
{
    [Fact]
    public void Mask_SendRequest_HidesPasswordRecipientAndText()
    {
        const string xml = "<SESSION><CLIENT>user</CLIENT><PW>secret</PW><MSGLST><MSG><ID>1</ID><TEXT>Kode: 123456</TEXT><SND>Altinn</SND><RCV>+4799123456</RCV></MSG></MSGLST></SESSION>";

        var masked = PayloadMasker.Mask(xml);

        Assert.Equal("<SESSION><CLIENT>user</CLIENT><PW>***</PW><MSGLST><MSG><ID>1</ID><TEXT>[masked, 12 chars]</TEXT><SND>Altinn</SND><RCV>+********56</RCV></MSG></MSGLST></SESSION>", masked);
        Assert.DoesNotContain("secret", masked);
        Assert.DoesNotContain("123456", masked);
    }

    [Fact]
    public void Mask_MultilineTextAndSeveralMessages_MasksEach()
    {
        const string xml = "<MSG><TEXT>line one\nline two</TEXT><RCV>4791111111</RCV></MSG><MSG><TEXT>b</TEXT><RCV>4792222222</RCV></MSG>";

        var masked = PayloadMasker.Mask(xml);

        Assert.Equal("<MSG><TEXT>[masked, 17 chars]</TEXT><RCV>********11</RCV></MSG><MSG><TEXT>[masked, 1 chars]</TEXT><RCV>********22</RCV></MSG>", masked);
    }

    [Fact]
    public void Mask_ResponseWithoutPersonalData_IsUnchanged()
    {
        const string xml = "<SESSION><LOGON>OK</LOGON><MSGLST><MSG><ID>1</ID><REF>abc</REF><STATUS>OK</STATUS></MSG></MSGLST></SESSION>";

        Assert.Equal(xml, PayloadMasker.Mask(xml));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Mask_NoBody_ReturnsEmpty(string? xml)
    {
        Assert.Equal(string.Empty, PayloadMasker.Mask(xml));
    }
}
