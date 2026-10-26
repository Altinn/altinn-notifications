using System.Reflection;

namespace Altinn.Notifications.Aspire.Hosting;

/// <summary>
/// The project files of the mocks under <c>tools/src</c>, which the app host builds and starts from source.
/// </summary>
internal static class MockProjects
{
    private static readonly string _toolsSourceDirectory = typeof(MockProjects).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "ToolsSourceDirectory").Value!;

    public static string MockAuth => Path("MockAuth");

    public static string AcsMock => Path("AcsMock");

    public static string LinkMobilityProxy => Path("LinkMobilityProxy");

    public static string PlatformMocks => Path("PlatformMocks");

    public static string TestHarness => Path("TestHarness");

    private static string Path(string tool) =>
        System.IO.Path.Combine(_toolsSourceDirectory, $"Altinn.Notifications.Tools.{tool}", $"Altinn.Notifications.Tools.{tool}.csproj");
}
