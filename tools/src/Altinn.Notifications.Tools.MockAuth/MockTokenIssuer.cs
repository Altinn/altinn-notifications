using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Notifications.Tools.MockAuth;

/// <summary>
/// Issues Altinn-style tokens signed with the API tests' self-signed certificate, and publishes its public key as a JWKS.
/// </summary>
internal sealed class MockTokenIssuer
{
    internal const string CertificateFile = "jwtselfsignedcert.pfx";
    private const string CertificatePassword = "qwer1234";
    private const string ClaimIssuer = "www.altinn.no";

    private readonly X509SecurityKey _key;
    private readonly SigningCredentials _signingCredentials;

    public MockTokenIssuer()
    {
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(AppContext.BaseDirectory, CertificateFile), CertificatePassword);
        _key = new X509SecurityKey(certificate);
        _signingCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// The key the tokens are signed with, for validating them in tests.
    /// </summary>
    internal SecurityKey SigningKey => _key;

    /// <summary>
    /// Creates a service owner token with the same claims as <c>PrincipalUtil.GetOrgToken</c> in the API tests.
    /// </summary>
    public string CreateEnterpriseToken(string issuer, string org, string orgNumber, string? scopes)
    {
        var identity = new ClaimsIdentity("mock-org");
        identity.AddClaim(new Claim("urn:altinn:org", org, ClaimValueTypes.String, ClaimIssuer));
        if (!string.IsNullOrEmpty(scopes))
        {
            identity.AddClaim(new Claim("urn:altinn:scope", scopes, ClaimValueTypes.String, "maskinporten"));
        }

        identity.AddClaim(new Claim("urn:altinn:orgNumber", orgNumber, ClaimValueTypes.Integer32, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authenticatemethod", "Mock", ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authenticationlevel", "3", ClaimValueTypes.Integer32, ClaimIssuer));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = issuer,
            Audience = "altinn.no",
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = _signingCredentials,
        });
    }

    /// <summary>
    /// The public signing key as a JSON Web Key Set.
    /// </summary>
    public object GetJwks()
    {
        var jwk = JsonWebKeyConverter.ConvertFromX509SecurityKey(_key, representAsRsaKey: true);
        return new
        {
            keys = new[]
            {
                new { kty = jwk.Kty, use = "sig", alg = SecurityAlgorithms.RsaSha256, kid = jwk.Kid, n = jwk.N, e = jwk.E },
            },
        };
    }
}
