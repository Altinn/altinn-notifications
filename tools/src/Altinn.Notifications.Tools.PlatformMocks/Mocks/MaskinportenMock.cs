using System.Text;
using System.Text.Json;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Notifications.Tools.PlatformMocks.Mocks;

/// <summary>
/// The token endpoint in Maskinporten, which the API's condition client gets its token from. It issues a token for any signed
/// grant without checking the signature, since the condition mock does not check the token either.
/// </summary>
internal sealed class MaskinportenMock : IPlatformMock
{
    internal const string TokenPath = "/token";

    internal const int ExpiresInSeconds = 120;

    public string System => "maskinporten";

    public void AddMappings(WireMockServer server)
    {
        server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(request => MockJson.Write(Token(Form(request.Body)))));
    }

    /// <summary>
    /// The scope comes from the grant, so the token looks like one for the client that asked.
    /// </summary>
    private static object Token(IReadOnlyDictionary<string, string> form)
    {
        string scope = form.TryGetValue("assertion", out var assertion) ? Claim(assertion, "scope") ?? string.Empty : string.Empty;
        return new Dictionary<string, object>
        {
            ["access_token"] = $"mock-maskinporten-token-{Guid.NewGuid():N}",
            ["token_type"] = "Bearer",
            ["expires_in"] = ExpiresInSeconds,
            ["scope"] = scope,
        };
    }

    private static Dictionary<string, string> Form(string? body)
    {
        return (body ?? string.Empty)
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty);
    }

    private static string? Claim(string jwt, string name)
    {
        string[] parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var claims = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return claims.RootElement.TryGetProperty(name, out var value) ? value.GetString() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
