using Xunit;

namespace Altinn.Notifications.Tests;

public static class AssertUtils
{
    /// <summary>
    /// Checks equivalency of two object of type T
    /// </summary>
    public static bool AreEquivalent<T>(T expected, T actual)
    {
        Assert.Equivalent(expected, actual);
        return true;
    }
}