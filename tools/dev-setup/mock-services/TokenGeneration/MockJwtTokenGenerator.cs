using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

namespace Altinn.Notifications.MockServices.TokenGeneration;

/// <summary>
/// Generates mock JWT tokens and exposes JWKS for OpenID Connect discovery.
/// </summary>
public class MockJwtTokenGenerator
{
    private readonly X509Certificate2 _certificate;
    private readonly X509SigningCredentials _signingCredentials;

    public MockJwtTokenGenerator(string certPath, string certPassword)
    {
        _certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
        _signingCredentials = new X509SigningCredentials(_certificate, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// Generates an enterprise (org) JWT token matching the pattern from PrincipalUtil.GetOrgToken.
    /// </summary>
    public string GenerateEnterpriseToken(string org, string scopes, string orgNumber = "991825827")
    {
        string issuer = "www.altinn.no";

        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(org))
        {
            claims.Add(new Claim("urn:altinn:org", org, ClaimValueTypes.String, issuer));
        }

        if (!string.IsNullOrEmpty(scopes))
        {
            claims.Add(new Claim("urn:altinn:scope", scopes, ClaimValueTypes.String, "maskinporten"));
        }

        claims.Add(new Claim("urn:altinn:orgNumber", orgNumber, ClaimValueTypes.Integer32, issuer));
        claims.Add(new Claim("urn:altinn:authenticatemethod", "Mock", ClaimValueTypes.String, issuer));
        claims.Add(new Claim("urn:altinn:authenticationlevel", "3", ClaimValueTypes.Integer32, issuer));

        var identity = new ClaimsIdentity("mock-org");
        identity.AddClaims(claims);
        var principal = new ClaimsPrincipal(identity);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(principal.Identity),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = _signingCredentials,
            Audience = "altinn.no",
            Issuer = "UnitTest",
        };

        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Returns a JWKS (JSON Web Key Set) document containing the public key from the certificate.
    /// </summary>
    public string GetJwks()
    {
        RSA rsa = _certificate.GetRSAPublicKey()!;
        RSAParameters rsaParams = rsa.ExportParameters(false);

        var jwk = new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            n = Base64UrlEncoder.Encode(rsaParams.Modulus!),
            e = Base64UrlEncoder.Encode(rsaParams.Exponent!),
            kid = _signingCredentials.Kid,
            x5t = Base64UrlEncoder.Encode(_certificate.GetCertHash()),
        };

        var jwks = new
        {
            keys = new[] { jwk },
        };

        return JsonSerializer.Serialize(jwks, new JsonSerializerOptions { WriteIndented = true });
    }
}
