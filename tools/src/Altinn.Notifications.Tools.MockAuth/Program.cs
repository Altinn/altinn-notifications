using System.Text;

using Altinn.Notifications.Tools.MockAuth;
using Altinn.Notifications.Tools.MockAuth.Configuration;

using Microsoft.Extensions.Options;

const string OpenIdPath = "/authentication/api/v1/openid";

// appsettings.json is in the output folder, so it is found whichever folder the issuer is started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Configuration
    .AddUserSecrets<MockAuthSettings>(optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<MockAuthSettings>(builder.Configuration.GetSection(nameof(MockAuthSettings)));
builder.Services.AddSingleton<MockTokenIssuer>();

var port = builder.Configuration.GetValue("MockAuthSettings:Port", 5094);
builder.WebHost.UseUrls($"http://localhost:{port}");

var app = builder.Build();

// Same shape as the Altinn test token generator, so the Bruno request "test-util/new auth token" works unchanged.
app.MapGet("/api/GetEnterpriseToken", (HttpContext context, MockTokenIssuer issuer, IOptions<MockAuthSettings> settings, string? org, string? orgNumber, string? scopes) =>
{
    if (!HasBasicAuth(context, settings.Value))
    {
        context.Response.Headers.WWWAuthenticate = "Basic";
        return Results.Unauthorized();
    }

    return Results.Text(issuer.CreateEnterpriseToken(BaseUrl(context), org ?? "ttd", orgNumber ?? "991825827", scopes));
});

// An end user token, like the test token generator's personal token. Bruno's "test-util/end user dialog token" asks for one
// with the scopes the end user notification log and Dialogporten need.
app.MapGet("/api/GetPersonalToken", (HttpContext context, MockTokenIssuer issuer, IOptions<MockAuthSettings> settings, int? userId, int? partyId, string? pid, int? authLvl, string? scopes) =>
{
    if (!HasBasicAuth(context, settings.Value))
    {
        context.Response.Headers.WWWAuthenticate = "Basic";
        return Results.Unauthorized();
    }

    return Results.Text(issuer.CreatePersonalToken(BaseUrl(context), userId ?? 20000000, partyId ?? 50000000, pid ?? "12345678901", authLvl ?? 3, scopes));
});

app.MapGet($"{OpenIdPath}/.well-known/openid-configuration", (HttpContext context) =>
{
    string baseUrl = BaseUrl(context);
    return Results.Json(new
    {
        issuer = baseUrl,
        jwks_uri = $"{baseUrl}{OpenIdPath}/jwks",
        id_token_signing_alg_values_supported = new[] { "RS256" },
        subject_types_supported = new[] { "public" },
        response_types_supported = new[] { "code" },
    });
});

app.MapGet($"{OpenIdPath}/jwks", (MockTokenIssuer issuer) => Results.Json(issuer.GetJwks()));

app.Run();

static string BaseUrl(HttpContext context) => $"{context.Request.Scheme}://{context.Request.Host}";

static bool HasBasicAuth(HttpContext context, MockAuthSettings settings)
{
    string? header = context.Request.Headers.Authorization;
    if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    try
    {
        string credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        return credentials == $"{settings.Username}:{settings.Password}";
    }
    catch (FormatException)
    {
        return false;
    }
}
