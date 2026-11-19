using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;

namespace Aspire.Hosting;

/// <summary>
/// Adds the ACS mock, which stands in for Azure Communication Services Email.
/// </summary>
public static class AcsMockBuilderExtensions
{
    // 44 letters and digits are valid base64, which the email service's SDK and the mock decode into the signing key.
    private const int _accessKeyLength = 44;

    /// <summary>
    /// Adds the ACS mock on <paramref name="port"/>, as a child of the test harness control panel. It answers by recipient address and
    /// sends a copy of each email and its outcome to the control panel's Inbox tab. It puts delivery reports on the Service Bus
    /// emulator's queue, so make it wait for the emulator. The key requests have to be signed with is the secret parameter
    /// <c>&lt;name&gt;-access-key</c>, generated for the checkout.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The localhost port the mock listens on.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    public static IResourceBuilder<DotnetProjectResource> AddAcsMock(this IDistributedApplicationBuilder builder, string name, int port, IResourceBuilder<DotnetProjectResource> testHarness)
    {
        var accessKey = builder.AddGeneratedSecret($"{name}-access-key", _accessKeyLength);
        var acs = builder.AddDotnetProject(name, MockProjects.AcsMock, options => options.ExcludeLaunchProfile = true)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
            .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithEnvironment("AcsMockSettings__Port", port.ToString())
            .WithEnvironment("AcsMockSettings__AccessKey", accessKey)
            .WithAnnotation(new AcsMockAnnotation(accessKey.Resource))
            .WithTestHarness(testHarness, "AcsMockSettings", mirror: true);

        accessKey.WithParentRelationship(acs);
        return acs;
    }

    /// <summary>
    /// Makes the email service send to <paramref name="acs"/> instead of the ACS resource in its own settings.
    /// </summary>
    /// <param name="email">The email service.</param>
    /// <param name="acs">The ACS mock.</param>
    public static IResourceBuilder<DotnetProjectResource> WithAcsMock(this IResourceBuilder<DotnetProjectResource> email, IResourceBuilder<DotnetProjectResource> acs)
    {
        if (!acs.Resource.TryGetLastAnnotation<AcsMockAnnotation>(out var mock))
        {
            throw new ArgumentException($"{acs.Resource.Name} is not the ACS mock. Add it with AddAcsMock.", nameof(acs));
        }

        return email.WithEnvironment("CommunicationServicesSettings__ConnectionString", ReferenceExpression.Create($"endpoint={acs.GetEndpoint("http")}/;accesskey={mock.AccessKey}"))
            .WaitFor(acs);
    }

    /// <summary>
    /// The key the mock checks request signatures with.
    /// </summary>
    private sealed record AcsMockAnnotation(ParameterResource AccessKey) : IResourceAnnotation;
}
