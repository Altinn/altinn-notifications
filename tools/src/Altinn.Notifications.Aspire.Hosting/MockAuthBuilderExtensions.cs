using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Adds the mock token issuer, which issues Altinn tokens that a service trusts instead of at23's.
/// </summary>
public static class MockAuthBuilderExtensions
{
    private const string _openIdPath = "/authentication/api/v1/openid/";

    /// <summary>
    /// Adds the mock token issuer on <paramref name="port"/>, as a child of the test harness control panel.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The localhost port the issuer listens on.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    public static IResourceBuilder<ProjectResource> AddMockAuth(this IDistributedApplicationBuilder builder, string name, int port, IResourceBuilder<ProjectResource> testHarness)
    {
        return builder.AddProject(name, MockProjects.MockAuth, launchProfileName: null)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
            .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrl("http://localhost:" + port + _openIdPath + ".well-known/openid-configuration", "OpenID configuration")
            .WithEnvironment("MockAuthSettings__Port", port.ToString())
            .WithParentRelationship(testHarness);
    }

    /// <summary>
    /// Makes <paramref name="service"/> trust tokens from <paramref name="auth"/> instead of the issuer in its own settings.
    /// </summary>
    /// <param name="service">A service that validates Altinn tokens with <c>GeneralSettings:OpenIdWellKnownEndpoint</c>.</param>
    /// <param name="auth">The mock token issuer.</param>
    public static IResourceBuilder<ProjectResource> WithMockAuth(this IResourceBuilder<ProjectResource> service, IResourceBuilder<ProjectResource> auth)
    {
        return service.WithEnvironment("GeneralSettings__OpenIdWellKnownEndpoint", ReferenceExpression.Create($"{auth.GetEndpoint("http")}{_openIdPath}"))
            .WaitFor(auth);
    }
}
