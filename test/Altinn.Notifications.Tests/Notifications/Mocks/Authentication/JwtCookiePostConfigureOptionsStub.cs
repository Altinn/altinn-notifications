using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

/// <summary>
/// Represents a stub for the <see cref="JwtCookiePostConfigureOptions"/> class to be used in integration tests.
/// </summary>
public class JwtCookiePostConfigureOptionsStub : IPostConfigureOptions<JwtCookieOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, JwtCookieOptions options)
    {
        if (string.IsNullOrEmpty(options.JwtCookieName))
        {
            options.JwtCookieName = JwtCookieDefaults.CookiePrefix + name;
        }

        options.CookieManager ??= new ChunkingCookieManager();

        if (!string.IsNullOrEmpty(options.MetadataAddress) && !options.MetadataAddress.EndsWith('/'))
        {
            options.MetadataAddress += "/";
        }

        options.MetadataAddress += ".well-known/openid-configuration";
        options.ConfigurationManager = new ConfigurationManagerStub();
    }
}
