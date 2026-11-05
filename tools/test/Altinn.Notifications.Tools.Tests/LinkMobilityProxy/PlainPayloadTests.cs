using System.Text;

using Altinn.Notifications.Tools.LinkMobilityProxy;

using WireMock.Types;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.LinkMobilityProxy;

public class PlainPayloadTests
{
    [Fact]
    public void Escape_ControlAndInvisibleCharacters_AreVisible()
    {
        const string text = "a\r\n\t\u0007\u001B\u00A0\u00AD\u200B\uFEFF\uFFFD\\n æøå 😀 e\u0301";

        Assert.Equal(@"a\r\n\t\u0007\u001B[U+00A0][U+00AD][U+200B][U+FEFF][U+FFFD]\\n æøå 😀 e[U+0301]", PlainPayload.Escape(text));
    }

    [Fact]
    public void MaskPassword_Bytes_MasksOnlyEachPasswordAndKeepsInvalidBytes()
    {
        byte[] body = [.. "<PW>secret</PW><TEXT>"u8, 0xFF, 0xE6, .. "</TEXT><PW>other</PW>"u8];

        byte[] masked = PayloadMasker.MaskPassword(body);

        Assert.Equal([.. "<PW>***</PW><TEXT>"u8, 0xFF, 0xE6, .. "</TEXT><PW>***</PW>"u8], masked);
    }

    [Fact]
    public void MaskPassword_Bytes_PasswordWithoutEnd_IsMaskedToTheEnd()
    {
        Assert.Equal("<CLIENT>u</CLIENT><PW>***"u8.ToArray(), PayloadMasker.MaskPassword("<CLIENT>u</CLIENT><PW>secret"u8));
    }

    [Fact]
    public void Describe_InvalidUtf8_ShowsReplacementInTextAndOriginalBytesInHex()
    {
        byte[] body = [.. "<PW>secret</PW><TEXT>a"u8, 0xFF, .. "\u0007</TEXT>"u8];

        string described = PlainPayload.Describe("Request", body, "text/xml; charset=utf-8");

        Assert.StartsWith($"Request: text/xml; charset=utf-8, {body.Length} bytes, decoded as utf-8\n<PW>***</PW><TEXT>a\uFFFD\u0007</TEXT>\n", described);
        Assert.Contains(@"Escaped: <PW>***</PW><TEXT>a[U+FFFD]\u0007</TEXT>", described);
        Assert.Contains("Hex: 3C 50 57 3E 2A 2A 2A 3C 2F 50 57 3E 3C 54 45 58 54 3E 61 FF 07 3C", described);
        Assert.DoesNotContain("secret", described);
    }

    [Fact]
    public void Describe_Latin1Charset_DecodesByCharset()
    {
        byte[] body = Encoding.Latin1.GetBytes("<TEXT>æøå</TEXT>");

        string described = PlainPayload.Describe("Response", body, "text/xml; charset=iso-8859-1");

        Assert.Contains("decoded as iso-8859-1\n<TEXT>æøå</TEXT>\n", described);
        Assert.Contains("Hex: 3C 54 45 58 54 3E E6 F8 E5 3C", described);
    }

    [Fact]
    public void Describe_PasswordNotFoundInBytes_LeavesOutHexButMasksText()
    {
        byte[] body = Encoding.Unicode.GetBytes("<PW>secret</PW><RCV>4799123456</RCV>");

        string described = PlainPayload.Describe("Request", body, "text/xml; charset=utf-16");

        Assert.Contains("<PW>***</PW><RCV>4799123456</RCV>", described);
        Assert.Contains("Hex: [left out: the password could not be masked in the bytes]", described);
        Assert.DoesNotContain("secret", described);
        Assert.Null(PlainPayload.MaskedBytes(body, "text/xml; charset=utf-16"));
    }

    [Fact]
    public void Describe_NoBody_SaysSo()
    {
        Assert.Equal("Response: no body", PlainPayload.Describe("Response", [], null));
    }

    [Fact]
    public void RequestHead_ShowsRequestLineAndHeadersLikeCurl_WithSecretsMasked()
    {
        var headers = new Dictionary<string, WireMockList<string>>
        {
            ["Host"] = new("localhost:5093"),
            ["Content-Type"] = new("text/xml; charset=utf-8"),
            ["authorization"] = new("Basic c2VjcmV0"),
            ["Accept"] = new("text/xml", "*/*"),
        };

        string head = PlainPayload.RequestHead("POST", "/?a=1", "1.1", headers);

        Assert.Equal("> POST /?a=1 HTTP/1.1\n> Host: localhost:5093\n> Content-Type: text/xml; charset=utf-8\n> authorization: ***\n> Accept: text/xml\n> Accept: */*", head);
    }

    [Theory]
    [InlineData(200, "1.1", "< HTTP/1.1 200 OK")]
    [InlineData(404, "1.1", "< HTTP/1.1 404 Not Found")]
    [InlineData("502", "HTTP/2", "< HTTP/2 502 Bad Gateway")]
    [InlineData(599, "1.1", "< HTTP/1.1 599")]
    public void ResponseHead_ShowsStatusLineWithReasonPhrase(object statusCode, string httpVersion, string expected)
    {
        Assert.Equal(expected, PlainPayload.ResponseHead(statusCode, httpVersion, null));
    }

    [Fact]
    public void ResponseHead_ShowsHeadersWithSetCookieMasked()
    {
        var headers = new Dictionary<string, WireMockList<string>> { ["Content-Type"] = new("text/xml"), ["Set-Cookie"] = new("session=abc") };

        Assert.Equal("< HTTP/1.1 200 OK\n< Content-Type: text/xml\n< Set-Cookie: ***", PlainPayload.ResponseHead(200, "1.1", headers));
    }
}
