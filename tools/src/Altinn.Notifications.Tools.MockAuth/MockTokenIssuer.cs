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
        if (NormalizeScopes(scopes) is string scope)
        {
            identity.AddClaim(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
        }

        identity.AddClaim(new Claim("urn:altinn:orgNumber", orgNumber, ClaimValueTypes.Integer32, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authenticatemethod", "Mock", ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authlevel", "3", ClaimValueTypes.Integer32, ClaimIssuer));

        return CreateToken(identity, issuer);
    }

    /// <summary>
    /// Creates an end user token with the same claims as <c>PrincipalUtil.GetUserToken</c> in the API tests, plus the
    /// <c>pid</c> and <c>scope</c> claims a token from ID-porten carries, like the Altinn test token generator's personal token.
    /// The scopes go in <c>scope</c>, where the API's scope check looks when there is no <c>urn:altinn:scope</c>.
    /// </summary>
    public string CreatePersonalToken(string issuer, int userId, int partyId, string pid, int authenticationLevel, string? scopes)
    {
        var identity = new ClaimsIdentity("mock");
        identity.AddClaim(new Claim("urn:altinn:userid", userId.ToString(), ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:username", "UserOne", ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:partyid", partyId.ToString(), ClaimValueTypes.Integer32, ClaimIssuer));
        identity.AddClaim(new Claim("pid", pid, ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authenticatemethod", "Mock", ClaimValueTypes.String, ClaimIssuer));
        identity.AddClaim(new Claim("urn:altinn:authlevel", authenticationLevel.ToString(), ClaimValueTypes.Integer32, ClaimIssuer));
        if (NormalizeScopes(scopes) is string scope)
        {
            identity.AddClaim(new Claim("scope", scope, ClaimValueTypes.String, ClaimIssuer));
        }

        return CreateToken(identity, issuer);
    }

    /// <summary>
    /// The test token generator takes scopes separated by commas or spaces. A token carries them separated by spaces.
    /// </summary>
    internal static string? NormalizeScopes(string? scopes)
    {
        string[] parts = (scopes ?? string.Empty).Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : string.Join(' ', parts);
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

    private string CreateToken(ClaimsIdentity identity, string issuer)
    {
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = issuer,
            Audience = "altinn.no",
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = _signingCredentials,
        });
    }
}
