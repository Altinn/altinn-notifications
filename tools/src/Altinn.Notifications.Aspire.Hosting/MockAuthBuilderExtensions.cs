using System.Security.Cryptography;

using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;

namespace Aspire.Hosting;

/// <summary>
/// Adds the mock token issuer, which issues Altinn tokens that a service trusts instead of at23's.
/// </summary>
public static class MockAuthBuilderExtensions
{
    private const string _openIdPath = "/authentication/api/v1/openid/";

    /// <summary>
    /// Adds the mock token issuer on <paramref name="port"/>, as a child of the test harness control panel. It signs with a key made
    /// for this run of the app host, so tokens stay valid when only the issuer restarts.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The localhost port the issuer listens on.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    public static IResourceBuilder<DotnetProjectResource> AddMockAuth(this IDistributedApplicationBuilder builder, string name, int port, IResourceBuilder<DotnetProjectResource> testHarness)
    {
        return builder.AddDotnetProject(name, MockProjects.MockAuth, options => options.ExcludeLaunchProfile = true)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
            .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrl(Localhost.Url(port) + _openIdPath + ".well-known/openid-configuration", "OpenID configuration")
            .WithEnvironment("MockAuthSettings__Port", port.ToString())
            .WithEnvironment("MockAuthSettings__SigningKey", CreateSigningKey())
            .WithParentRelationship(testHarness);
    }

    /// <summary>
    /// Makes <paramref name="service"/> trust tokens from <paramref name="auth"/> instead of the issuer in its own settings.
    /// </summary>
    /// <param name="service">A service that validates Altinn tokens with <c>GeneralSettings:OpenIdWellKnownEndpoint</c>.</param>
    /// <param name="auth">The mock token issuer.</param>
    public static IResourceBuilder<DotnetProjectResource> WithMockAuth(this IResourceBuilder<DotnetProjectResource> service, IResourceBuilder<DotnetProjectResource> auth)
    {
        return service.WithEnvironment("GeneralSettings__OpenIdWellKnownEndpoint", ReferenceExpression.Create($"{auth.GetEndpoint("http")}{_openIdPath}"))
            .WaitFor(auth);
    }

    // A throwaway RSA key as a base64-encoded PKCS#8 private key, the format MockAuthSettings:SigningKey takes.
    private static string CreateSigningKey()
    {
        using var rsa = RSA.Create(2048);
        return Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
    }
}
