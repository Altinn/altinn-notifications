using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Adds the ACS mock, which stands in for Azure Communication Services Email.
/// </summary>
public static class AcsMockBuilderExtensions
{
    /// <summary>
    /// A test-only key. The email service signs with it, and the mock rejects requests that are not signed with it.
    /// </summary>
    internal const string AccessKey = "YWx0aW5uLW5vdGlmaWNhdGlvbnMtYWNzLW1vY2stdGVzdC1rZXk=";

    /// <summary>
    /// Adds the ACS mock on <paramref name="port"/>, as a child of the test harness control panel. It answers by recipient address and
    /// sends a copy of each email and its outcome to the control panel's Inbox tab. It puts delivery reports on the Service Bus
    /// emulator's queue, so make it wait for the emulator.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The localhost port the mock listens on.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    public static IResourceBuilder<ProjectResource> AddAcsMock(this IDistributedApplicationBuilder builder, string name, int port, IResourceBuilder<ProjectResource> testHarness)
    {
        return builder.AddProject(name, MockProjects.AcsMock, launchProfileName: null)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
            .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithEnvironment("AcsMockSettings__Port", port.ToString())
            .WithEnvironment("AcsMockSettings__AccessKey", AccessKey)
            .WithTestHarness(testHarness, "AcsMockSettings", mirror: true);
    }

    /// <summary>
    /// Makes the email service send to <paramref name="acs"/> instead of the ACS resource in its own settings.
    /// </summary>
    /// <param name="email">The email service.</param>
    /// <param name="acs">The ACS mock.</param>
    public static IResourceBuilder<ProjectResource> WithAcsMock(this IResourceBuilder<ProjectResource> email, IResourceBuilder<ProjectResource> acs)
    {
        return email.WithEnvironment("CommunicationServicesSettings__ConnectionString", ReferenceExpression.Create($"endpoint={acs.GetEndpoint("http")}/;accesskey={AccessKey}"))
            .WaitFor(acs);
    }
}
