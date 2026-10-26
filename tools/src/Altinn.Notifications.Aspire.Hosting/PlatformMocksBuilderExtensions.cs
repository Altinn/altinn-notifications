using System.Security.Cryptography;
using System.Text.Json;

using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;

using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;

/// <summary>
/// Adds the platform mocks, which stand in for Profile, Register, Authorization, Dialogporten, Maskinporten and condition endpoints.
/// </summary>
public static class PlatformMocksBuilderExtensions
{
    /// <summary>
    /// Each mocked system, its default port, and the API setting and path that point the API to it.
    /// </summary>
    private static readonly (string Name, int Port, string ApiSetting, string ApiPath)[] _systems =
    [
        ("Profile", 5131, "PlatformSettings__ApiProfileEndpoint", "/profile/api/v1/"),
        ("Register", 5121, "PlatformSettings__ApiRegisterEndpoint", "/register/api/v1/"),
        ("Authorization", 5151, "PlatformSettings__ApiAuthorizationEndpoint", "/authorization/api/v1/"),
        ("Dialogporten", 5171, "DialogportenSettings__BaseUrl", "/dialogporten/api/v1/"),
        ("Conditions", 5199, string.Empty, string.Empty), // Orders point to these themselves.
        ("Maskinporten", 5197, string.Empty, string.Empty), // See WithPlatformMocks.
    ];

    /// <summary>
    /// Adds the platform mocks as a child of the test harness control panel. Each system can be turned off or moved to another port
    /// with <c>&lt;system&gt;:Enabled</c> and <c>&lt;system&gt;:Port</c> in <paramref name="settings"/>.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    /// <param name="settings">The systems' settings, e.g. the app host's <c>Mocks</c> section.</param>
    public static IResourceBuilder<ProjectResource> AddPlatformMocks(this IDistributedApplicationBuilder builder, string name, IResourceBuilder<ProjectResource> testHarness, IConfiguration settings)
    {
        var systems = _systems.Select(s => new PlatformMock(
            s.Name,
            settings.GetValue($"{s.Name}:Enabled", true),
            settings.GetValue($"{s.Name}:Port", s.Port),
            s.ApiSetting,
            s.ApiPath)).ToList();

        var mocks = builder.AddProject(name, MockProjects.PlatformMocks, launchProfileName: null)
            .WithAnnotation(new PlatformMocksAnnotation(systems))
            .WithTestHarness(testHarness, "PlatformMocksSettings", mirror: false);

        foreach (var system in systems)
        {
            mocks.WithEnvironment($"PlatformMocksSettings__{system.Name}__Enabled", system.Enabled.ToString())
                .WithEnvironment($"PlatformMocksSettings__{system.Name}__Port", system.Port.ToString());
        }

        if (systems.Single(s => s.Name == "Conditions") is { Enabled: true } conditions)
        {
            // The API only accepts condition URLs with a top-level domain, so the URLs to put in orders use local.altinn.cloud, which resolves to 127.0.0.1.
            foreach (string condition in new[] { "true", "false", "corrupt", "error" })
            {
                mocks.WithUrl("http://local.altinn.cloud:" + conditions.Port + "/condition/" + condition, "Condition " + condition);
            }
        }

        return mocks;
    }

    /// <summary>
    /// Points the API's calls to the enabled systems to <paramref name="mocks"/>. A system that is turned off keeps the endpoint from
    /// the API's own settings, e.g. a local altinn-profile on 5030.
    /// </summary>
    /// <param name="api">The API.</param>
    /// <param name="mocks">The platform mocks.</param>
    public static IResourceBuilder<ProjectResource> WithPlatformMocks(this IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> mocks)
    {
        if (!mocks.Resource.TryGetLastAnnotation<PlatformMocksAnnotation>(out var annotation))
        {
            throw new ArgumentException($"{mocks.Resource.Name} is not the platform mocks. Add them with AddPlatformMocks.", nameof(mocks));
        }

        foreach (var system in annotation.Systems.Where(s => s.Enabled && s.ApiSetting.Length > 0))
        {
            api.WithEnvironment(system.ApiSetting, "http://localhost:" + system.Port + system.ApiPath);
        }

        // The condition client gets its token from the Maskinporten mock instead of test.maskinporten.no. The grant still has to be
        // signed, so each run makes a key of its own. The mock does not check it.
        if (annotation.Systems.Single(s => s.Name == "Maskinporten") is { Enabled: true } maskinporten)
        {
            string maskinportenUrl = "http://localhost:" + maskinporten.Port;
            api.WithEnvironment("SendConditionClient__MaskinportenSettings__TokenEndpoint", maskinportenUrl + "/token")
                .WithEnvironment("SendConditionClient__MaskinportenSettings__AssertionAudience", maskinportenUrl + "/")
                .WithEnvironment("SendConditionClient__MaskinportenSettings__ClientId", "altinn-notifications-local")
                .WithEnvironment("SendConditionClient__MaskinportenSettings__EncodedJwk", CreateSigningKey());
        }

        return api.WaitFor(mocks);
    }

    /// <summary>
    /// A throwaway RSA key as a base64-encoded JWK, the format <c>MaskinportenSettings:EncodedJwk</c> takes.
    /// </summary>
    internal static string CreateSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var key = rsa.ExportParameters(includePrivateParameters: true);
        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["kid"] = "altinn-notifications-local",
            ["alg"] = "RS256",
            ["use"] = "sig",
            ["e"] = Base64Url(key.Exponent),
            ["n"] = Base64Url(key.Modulus),
            ["d"] = Base64Url(key.D),
            ["p"] = Base64Url(key.P),
            ["q"] = Base64Url(key.Q),
            ["dp"] = Base64Url(key.DP),
            ["dq"] = Base64Url(key.DQ),
            ["qi"] = Base64Url(key.InverseQ),
        };

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(jwk));

        static string Base64Url(byte[]? bytes) => Convert.ToBase64String(bytes!).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record PlatformMock(string Name, bool Enabled, int Port, string ApiSetting, string ApiPath);

    private sealed record PlatformMocksAnnotation(IReadOnlyList<PlatformMock> Systems) : IResourceAnnotation;
}
