using System;
using System.Reflection;

using Altinn.Notifications.Tools.AcsMock.Configuration;
using Altinn.Notifications.Tools.TestHarness.Configuration;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.Shared;

/// <summary>
/// <c>tools/src/Shared/LogSanitizer.cs</c> is linked into each tool that logs request data, and each copy is tested here.
/// The copies are found by reflection: a direct reference would be ambiguous between the tools,
/// and a copy linked into this project would not count toward the tools' coverage.
/// </summary>
public class LogSanitizerTests
{
    // A type from each tool that links LogSanitizer.cs.
    private static readonly Type[] _tools = [typeof(AcsMockSettings), typeof(TestHarnessSettings)];

    private static readonly (string? Value, string Expected)[] _cases =
    [
        ("POST /emails:send?api-version=2025-09-01", "POST /emails:send?api-version=2025-09-01"),
        ("Ola Nordmann, Bærum", "Ola Nordmann, Bærum"),
        ("/path\r\nACS POST /emails:send -> 202", @"/path\r\nACS POST /emails:send -> 202"),
        ("a\tb", @"a\tb"),
        ("a\0b\u001bc", @"a\u0000b\u001bc"),
        ("line\x2028paragraph\x2029end", @"line\" + @"u2028paragraph\" + @"u2029end"),
        (string.Empty, string.Empty),
        (null, string.Empty),
    ];

    public static TheoryData<string, string?, string> Cases()
    {
        var data = new TheoryData<string, string?, string>();
        foreach (var tool in _tools)
        {
            foreach (var (value, expected) in _cases)
            {
                data.Add(tool.Assembly.GetName().Name!, value, expected);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Clean_EscapesCharactersThatCouldStartANewLogLine(string tool, string? value, string expected)
    {
        var clean = Assembly.Load(tool)
            .GetType("Altinn.Notifications.Tools.Shared.LogSanitizer", throwOnError: true)!
            .GetMethod("Clean", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Func<string?, string>>();

        Assert.Equal(expected, clean(value));
    }
}
