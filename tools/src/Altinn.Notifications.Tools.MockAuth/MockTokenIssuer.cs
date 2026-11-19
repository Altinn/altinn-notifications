using System.Security.Claims;
using System.Security.Cryptography;

using Altinn.Notifications.Tools.MockAuth.Configuration;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Notifications.Tools.MockAuth;

/// <summary>
/// Issues Altinn-style tokens signed with an RSA key that only exists in memory, and publishes its public key as a JWKS.
/// </summary>
internal sealed class MockTokenIssuer : IDisposable
{
    private const string ClaimIssuer = "www.altinn.no";

    private readonly RSA _rsa;
    private readonly RsaSecurityKey _key;
    private readonly SigningCredentials _signingCredentials;

    /// <summary>
    /// Uses the key in <see cref="MockAuthSettings.SigningKey"/>, or makes a new one when it is empty.
    /// </summary>
    public MockTokenIssuer(IOptions<MockAuthSettings> options)
    {
        _rsa = RSA.Create(2048);
        string signingKey = options.Value.SigningKey;
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            _rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(signingKey), out _);
        }

        // The key id is derived from the public key, so the same key gets the same id.
        _key = new RsaSecurityKey(_rsa);
        _key.KeyId = Base64UrlEncoder.Encode(_key.ComputeJwkThumbprint());
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
            // The Altinn test token generator takes a comma-separated list, like k6 sends, and the scope claim is space-separated.
            string scopeClaim = string.Join(' ', scopes.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries));
            identity.AddClaim(new Claim("urn:altinn:scope", scopeClaim, ClaimValueTypes.String, "maskinporten"));
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
        var parameters = _rsa.ExportParameters(includePrivateParameters: false);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = SecurityAlgorithms.RsaSha256,
                    kid = _key.KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent),
                },
            },
        };
    }

    public void Dispose() => _rsa.Dispose();
}
