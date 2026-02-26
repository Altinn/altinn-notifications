using System;

using Altinn.Notifications.Extensions;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingExtensions;

/// <summary>
/// Test class for Uri extension methods
/// </summary>
public class UriExtensionsTests
{
    [Theory]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.no", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("http://invalid-url", false)]
    [InlineData("https://foobar", false)]
    [InlineData("http://localhost", false)]
    [InlineData("http://127.0.0.1", false)]
    [InlineData("http://example", false)]
    public void IsValidUrl(string uriString, bool expected)
    {
        Uri uri = new Uri(uriString);
        var actual = uri.IsValidUrl();
        Assert.Equal(expected, actual);
    }
}
