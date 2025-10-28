using System.Runtime.CompilerServices;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Module initializer that ensures proper cleanup of shared resources when tests complete.
/// </summary>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Register cleanup handler when the test process exits
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        // Dispose the shared data source when all tests are done
        ServiceUtil.DisposeSharedDataSource();
    }
}
