using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Altinn.Notifications.Aspire.Hosting;

/// <summary>
/// Marks the test harness control panel, so the mocks added with it know where to send what they do not recognise and their copies.
/// </summary>
internal sealed record TestHarnessAnnotation(string Url, bool Intercept, int TimeoutSeconds) : IResourceAnnotation
{
    public static TestHarnessAnnotation Of(IResourceBuilder<ProjectResource> testHarness) =>
        testHarness.Resource.TryGetLastAnnotation<TestHarnessAnnotation>(out var annotation)
            ? annotation
            : throw new ArgumentException($"{testHarness.Resource.Name} is not a test harness. Add it with AddTestHarness.", nameof(testHarness));
}

/// <summary>
/// Hooks a mock up to the test harness control panel.
/// </summary>
internal static class TestHarnessLinks
{
    /// <summary>
    /// Makes <paramref name="mock"/> a child of the control panel in the dashboard. With <paramref name="mirror"/> the mock sends a copy
    /// of each message to the Phone or Inbox tab. With intercept on, the mock hands requests it does not recognise to the control
    /// panel, so it waits for it.
    /// </summary>
    /// <param name="mock">The mock.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    /// <param name="settingsSection">The mock's settings section, e.g. <c>AcsMockSettings</c>.</param>
    /// <param name="mirror">Whether the mock takes a <c>MirrorUrl</c>.</param>
    public static IResourceBuilder<ProjectResource> WithTestHarness(this IResourceBuilder<ProjectResource> mock, IResourceBuilder<ProjectResource> testHarness, string settingsSection, bool mirror)
    {
        var harness = TestHarnessAnnotation.Of(testHarness);
        mock.WithParentRelationship(testHarness)
            .WithEnvironment($"{settingsSection}__FallbackUrl", harness.Intercept ? harness.Url + "/intercept/" : string.Empty);

        if (mirror)
        {
            mock.WithEnvironment($"{settingsSection}__MirrorUrl", harness.Url + "/mirror/");
        }

        if (harness.Intercept)
        {
            mock.WaitFor(testHarness);
        }

        return mock;
    }
}
