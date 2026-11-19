using System.Security.Cryptography;

using Altinn.Notifications.Tools.MockAuth.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Altinn.Notifications.Tools.Tests.MockAuth;

/// <summary>
/// Runs the issuer in memory with Basic auth credentials made for the test run, like the ones the app host generates.
/// </summary>
public class MockAuthFactory : WebApplicationFactory<MockAuthSettings>
{
    public const string Username = "test-user";

    public static readonly string Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("MockAuthSettings:Username", Username);
        builder.UseSetting("MockAuthSettings:Password", Password);
    }
}
