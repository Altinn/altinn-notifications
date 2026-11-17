using Altinn.Notifications.Tools.Shared;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.Shared;

public class LogSanitizerTests
{
    [Theory]
    [InlineData("POST /emails:send?api-version=2025-09-01", "POST /emails:send?api-version=2025-09-01")]
    [InlineData("Ola Nordmann, Bærum", "Ola Nordmann, Bærum")]
    [InlineData("/path\r\nACS POST /emails:send -> 202", @"/path\r\nACS POST /emails:send -> 202")]
    [InlineData("a\tb", @"a\tb")]
    [InlineData("a\0b\u001bc", @"a\u0000b\u001bc")]
    [InlineData("line\x2028paragraph\x2029end", @"line\" + @"u2028paragraph\" + @"u2029end")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Clean_EscapesCharactersThatCouldStartANewLogLine(string? value, string expected)
    {
        Assert.Equal(expected, LogSanitizer.Clean(value));
    }
}
