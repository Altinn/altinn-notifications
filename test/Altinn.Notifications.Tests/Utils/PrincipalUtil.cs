using System;
using System.Collections.Generic;
using System.Security.Claims;

using Altinn.Notifications.Tests.Mocks.Authentication;

using AltinnCore.Authentication.Constants;

namespace Altinn.Notifications.Tests.Utils;

public static class PrincipalUtil
{

    public static ClaimsPrincipal GetClaimsPrincipal(string org, string orgNumber, string? scope = null)
    {
        string issuer = "www.altinn.no";

        List<Claim> claims = new();
        if (!string.IsNullOrEmpty(org))
        {
            claims.Add(new Claim(AltinnCoreClaimTypes.Org, org, ClaimValueTypes.String, issuer));
        }

        if (scope != null)
        {
            claims.Add(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
        }

        claims.Add(new Claim(AltinnCoreClaimTypes.OrgNumber, orgNumber.ToString(), ClaimValueTypes.Integer32, issuer));
        claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer));
        claims.Add(new Claim(AltinnCoreClaimTypes.AuthenticationLevel, "3", ClaimValueTypes.Integer32, issuer));

        ClaimsIdentity identity = new("mock-org");
        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal GetClaimsPrincipal(int userId, int authenticationLevel, string? scope = null)
    {
        string issuer = "www.altinn.no";

        List<Claim> claims = new()
        {
                new Claim(AltinnCoreClaimTypes.UserId, userId.ToString(), ClaimValueTypes.String, issuer),
                new Claim(AltinnCoreClaimTypes.UserName, "UserOne", ClaimValueTypes.String, issuer),
                new Claim(AltinnCoreClaimTypes.PartyID, userId.ToString(), ClaimValueTypes.Integer32, issuer),
                new Claim(AltinnCoreClaimTypes.AuthenticateMethod, "Mock", ClaimValueTypes.String, issuer),
                new Claim(AltinnCoreClaimTypes.AuthenticationLevel, authenticationLevel.ToString(), ClaimValueTypes.Integer32, issuer)
            };

        if (scope != null)
        {
            claims.Add(new Claim("urn:altinn:scope", scope, ClaimValueTypes.String, "maskinporten"));
        }

        ClaimsIdentity identity = new("mock");
        identity.AddClaims(claims);

        return new ClaimsPrincipal(identity);
    }

    public static string GetOrgToken(string org, string orgNumber = "991825827", string? scope = null)
    {
        ClaimsPrincipal principal = GetClaimsPrincipal(org, orgNumber, scope);

        string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(1, 1, 1));

        return token;
    }
        public static string GetUserToken(int userId, int authenticationLevel = 2, string? scope = null)
    {
        ClaimsPrincipal principal = GetClaimsPrincipal(userId, authenticationLevel, scope);

        string token = JwtTokenMock.GenerateToken(principal, new TimeSpan(0, 1, 5));

        return token;
    }
}
