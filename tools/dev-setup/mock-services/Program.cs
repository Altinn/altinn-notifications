using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.MockServices.Scheduling;
using Altinn.Notifications.MockServices.TokenGeneration;

using Microsoft.IdentityModel.Tokens;

using WireMock.Server;
using WireMock.Settings;

Console.WriteLine("Starting Altinn Notifications Mock Services...");
Console.WriteLine();

// Determine base path for mappings (works both from project root and output directory)
string basePath = AppContext.BaseDirectory;
string mappingsPath = Path.Combine(basePath, "mappings");
if (!Directory.Exists(mappingsPath))
{
    // Fallback: running from project directory
    mappingsPath = Path.Combine(Directory.GetCurrentDirectory(), "mappings");
}

// Start WireMock servers
var servers = new Dictionary<string, (int Port, string MappingsDir)>
{
    ["Profile"]       = (5030, "profile"),
    ["Register"]      = (5020, "register"),
    ["Authorization"] = (5050, "authorization"),
    ["Services"]      = (5092, "services"),
    ["Conditions"]    = (5199, "conditions"),
};

var wireMockServers = new List<WireMockServer>();

foreach (var (name, (port, dir)) in servers)
{
    var server = WireMockServer.Start(new WireMockServerSettings
    {
        Port = port,
    });

    string serviceDir = Path.Combine(mappingsPath, dir);
    if (Directory.Exists(serviceDir))
    {
        foreach (string file in Directory.GetFiles(serviceDir, "*.json"))
        {
            string json = File.ReadAllText(file);
            server.WithMapping(json);
            Console.WriteLine($"  Loaded mapping: {Path.GetFileName(file)}");
        }
    }

    wireMockServers.Add(server);
    Console.WriteLine($"  {name} mock started on port {port}");
}

Console.WriteLine();

// Resolve the certificate path — try several candidate locations so the cert is found
// whether we run from the build output directory, from repo root, or from mock-services/.
string[] certRelSegments = ["components", "api", "test", "Altinn.Notifications.Tests", "jwtselfsignedcert.pfx"];
string[] certCandidates =
[
    // From build output (bin/Debug/net9.0) → 6 levels up to repo root
    Path.GetFullPath(Path.Combine([basePath, "..", "..", "..", "..", "..", "..", .. certRelSegments])),
    // From repo root (start-mock-services.sh sets CWD = repo root)
    Path.GetFullPath(Path.Combine([Directory.GetCurrentDirectory(), .. certRelSegments])),
    // From mock-services directory (tools/dev-setup/mock-services → 3 levels up to repo root)
    Path.GetFullPath(Path.Combine([Directory.GetCurrentDirectory(), "..", "..", "..", .. certRelSegments])),
];

string? certPath = certCandidates.FirstOrDefault(File.Exists);
if (certPath is null)
{
    Console.Error.WriteLine("ERROR: Certificate not found. Searched:");
    foreach (string candidate in certCandidates)
    {
        Console.Error.WriteLine($"  - {candidate}");
    }

    Console.Error.WriteLine("Make sure you run from the repository root or the mock-services project directory.");
    return 1;
}

var tokenGenerator = new MockJwtTokenGenerator(certPath, "qwer1234");

// Start Kestrel for token and OpenID endpoints
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5101);
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddHostedService<TriggerScheduler>();

var app = builder.Build();

// GET /api/GetEnterpriseToken
app.MapGet("/api/GetEnterpriseToken", (HttpContext context) =>
{
    // Validate basic auth
    if (!ValidateBasicAuth(context, "mock", "mock"))
    {
        context.Response.StatusCode = 401;
        return Results.Text("Unauthorized", "text/plain", statusCode: 401);
    }

    string org = context.Request.Query["org"].FirstOrDefault() ?? "ttd";
    string scopes = context.Request.Query["scopes"].FirstOrDefault() ?? "";

    string token = tokenGenerator.GenerateEnterpriseToken(org, scopes);
    return Results.Text(token, "text/plain");
});

// OIDC Discovery endpoint
app.MapGet("/authentication/api/v1/openid/.well-known/openid-configuration", (HttpContext context) =>
{
    string baseUrl = "http://localhost:5101";
    var discovery = new
    {
        issuer = "UnitTest",
        authorization_endpoint = $"{baseUrl}/authentication/api/v1/openid/authorize",
        token_endpoint = $"{baseUrl}/authentication/api/v1/openid/token",
        jwks_uri = $"{baseUrl}/authentication/api/v1/openid/jwks",
        id_token_signing_alg_values_supported = new[] { "RS256" },
        subject_types_supported = new[] { "public" },
        response_types_supported = new[] { "code" },
    };

    return Results.Json(discovery);
});

// JWKS endpoint
app.MapGet("/authentication/api/v1/openid/jwks", () =>
{
    var jwks = tokenGenerator.GetJwks();
    return Results.Text(jwks, "application/json");
});

Console.WriteLine("  Token generator + OpenID started on port 5101");
Console.WriteLine();
Console.WriteLine("Mock services are running. Press Ctrl+C to stop.");
Console.WriteLine();
Console.WriteLine("Endpoints:");
Console.WriteLine("  Token:  http://localhost:5101/api/GetEnterpriseToken?env=local&scopes=altinn:serviceowner/notifications.create&org=ttd");
Console.WriteLine("  OIDC:   http://localhost:5101/authentication/api/v1/openid/.well-known/openid-configuration");
Console.WriteLine("  JWKS:   http://localhost:5101/authentication/api/v1/openid/jwks");

await app.RunAsync();

// Cleanup
foreach (var server in wireMockServers)
{
    server.Stop();
}

return 0;

static bool ValidateBasicAuth(HttpContext context, string expectedUser, string expectedPass)
{
    string? authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    string encoded = authHeader["Basic ".Length..].Trim();

    byte[] bytes;
    try
    {
        bytes = Convert.FromBase64String(encoded);
    }
    catch (FormatException)
    {
        return false;
    }

    string decoded = Encoding.UTF8.GetString(bytes);
    string[] parts = decoded.Split(':', 2);
    return parts.Length == 2 && parts[0] == expectedUser && parts[1] == expectedPass;
}
