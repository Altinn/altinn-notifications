using Altinn.Notifications.Tools.AcsMock;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.AcsMock;

public class PayloadMaskerTests
{
    [Fact]
    public void Mask_SendRequest_HidesRecipientsContentAndAttachments()
    {
        string json = """
            {"senderAddress":"noreply@altinn.example","content":{"subject":"Hei Ola","plainText":"Koden er 123456","html":"<p>123456</p>"},
             "recipients":{"to":[{"address":"ola.nordmann@example.com","displayName":"Ola Nordmann"}],"bcc":[{"address":"kari@example.no"}]},
             "attachments":[{"name":"a.pdf","contentType":"application/pdf","contentInBase64":"JVBERi0="}]}
            """;

        string masked = PayloadMasker.Mask(json);

        Assert.DoesNotContain("ola.nordmann", masked);
        Assert.DoesNotContain("Ola Nordmann", masked);
        Assert.DoesNotContain("kari", masked);
        Assert.DoesNotContain("123456", masked);
        Assert.DoesNotContain("JVBERi0=", masked);
        Assert.Contains("o***@example.com", masked);
        Assert.Contains("k***@example.no", masked);
        Assert.Contains("noreply@altinn.example", masked);
        Assert.Contains("[masked, 15 chars]", masked);
    }

    [Fact]
    public void Mask_DeliveryReport_HidesRecipient()
    {
        string masked = PayloadMasker.Mask("""{"data":{"recipient":"ola@example.com","status":"Delivered"}}""");

        Assert.Equal("""{"data":{"recipient":"o***@example.com","status":"Delivered"}}""", masked);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("not json ola@example.com", "[not JSON, 24 chars]")]
    public void Mask_EmptyOrNotJson_HidesEverything(string? input, string expected)
    {
        Assert.Equal(expected, PayloadMasker.Mask(input));
    }

    [Theory]
    [InlineData(new[] { "ola@example.com" }, EmailScenario.Delivered)]
    [InlineData(new[] { "ola@example.com", "Bounce+test@example.com" }, EmailScenario.Bounced)]
    [InlineData(new[] { "slow@example.com", "fail@example.com" }, EmailScenario.Slow)]
    [InlineData(new[] { "notfail@example.com" }, EmailScenario.Delivered)]
    public void Pick_UsesFirstRecipientWithKeyword(string[] recipients, object expected)
    {
        Assert.Equal((EmailScenario)expected, EmailScenarios.Pick(recipients));
    }
}
