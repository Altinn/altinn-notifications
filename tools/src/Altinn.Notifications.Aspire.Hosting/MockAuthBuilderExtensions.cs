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

    // Not a secret, like the user name of the Altinn test token generator. The password is generated, see GeneratedSecrets.
    private const string _username = "mock";

    /// <summary>
    /// Adds the mock token issuer on <paramref name="port"/>, as a child of the test harness control panel. It signs with a key made
    /// for this run of the app host, so tokens stay valid when only the issuer restarts. Its Basic auth password is the secret
    /// parameter <c>&lt;name&gt;-password</c>, generated for the checkout. Get it with <see cref="GetMockAuthCredentials"/>.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The localhost port the issuer listens on.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    public static IResourceBuilder<DotnetProjectResource> AddMockAuth(this IDistributedApplicationBuilder builder, string name, int port, IResourceBuilder<DotnetProjectResource> testHarness)
    {
        var password = builder.AddGeneratedSecret($"{name}-password", 32);
        var auth = builder.AddDotnetProject(name, MockProjects.MockAuth, options => options.ExcludeLaunchProfile = true)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
            .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrl(Localhost.Url(port) + _openIdPath + ".well-known/openid-configuration", "OpenID configuration")
            .WithEnvironment("MockAuthSettings__Port", port.ToString())
            .WithEnvironment("MockAuthSettings__SigningKey", CreateSigningKey())
            .WithEnvironment("MockAuthSettings__Username", _username)
            .WithEnvironment("MockAuthSettings__Password", password)
            .WithAnnotation(new MockAuthCredentials(_username, password.Resource))
            .WithParentRelationship(testHarness);

        password.WithParentRelationship(auth);
        return auth;
    }

    /// <summary>
    /// The Basic auth <paramref name="auth"/> takes, for clients outside Aspire such as Bruno and k6.
    /// </summary>
    /// <param name="auth">The mock token issuer from <see cref="AddMockAuth"/>.</param>
    public static MockAuthCredentials GetMockAuthCredentials(this IResourceBuilder<DotnetProjectResource> auth) =>
        auth.Resource.TryGetLastAnnotation<MockAuthCredentials>(out var credentials)
            ? credentials
            : throw new ArgumentException($"{auth.Resource.Name} is not the mock token issuer. Add it with AddMockAuth.", nameof(auth));

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
