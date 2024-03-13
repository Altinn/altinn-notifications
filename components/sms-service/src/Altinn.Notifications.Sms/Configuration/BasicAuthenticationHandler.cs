using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Altinn.Notifications.Sms.Configuration;

/// <summary>
/// Set up basic authentication handler for controller endpoints
/// </summary>
public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly SmsDeliveryReportSettings _smsDeliveryReportSettings;
    private readonly ILogger<BasicAuthenticationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">options</param>
    /// <param name="logger">logger</param>
    /// <param name="encoder">encoder</param>
    /// <param name="smsDeliveryReportSettings">userSettings</param>
    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SmsDeliveryReportSettings smsDeliveryReportSettings)
        : base(options, logger, encoder)
    {
        _smsDeliveryReportSettings = smsDeliveryReportSettings;
        _logger = logger.CreateLogger<BasicAuthenticationHandler>();
    }

    /// <summary>
    /// Authenticate the user
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Path.StartsWithSegments("/notifications/sms/api/v1/reports"))
        {
            // Bypass authentication for all endspoint not reports related
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string username = string.Empty;
        string password = string.Empty;

        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            _logger.LogError("// BasicAuthenticationHandler // HandleAuthenticateAsync // Missing Authorization Header.");

            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));
        }

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(authorizationHeader!);
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter!);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split([':'], 2);
            username = credentials[0];
            password = credentials[1];
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }

        if (username != _smsDeliveryReportSettings.UserSettings.Username || password != _smsDeliveryReportSettings.UserSettings.Password)
        {
            _logger.LogError("// BasicAuthenticationHandler // HandleAuthenticateAsync // Invalid Username {Username} or Password: {Password}", username, password[^5..]);
            return Task.FromResult(AuthenticateResult.Fail("Invalid Username or Password"));
        }

        var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
            };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
