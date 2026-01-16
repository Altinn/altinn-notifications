using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Altinn.Notifications.Authorization
{
    /// <summary>
    /// Authorization filter to validate API key
    /// </summary>
    public class MetricsApiKeyFilter(
        IConfiguration configuration,
        ILogger<MetricsApiKeyFilter> logger) : IAsyncAuthorizationFilter
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<MetricsApiKeyFilter> _logger = logger;

        /// <summary>
        /// Authorization filter to validate API key metrics endpoints
        /// </summary>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Only apply to Metrics endpoints
            if (!context.HttpContext.Request.Path.StartsWithSegments("/notifications/api/v1/metrics/sms"))
            {
                return; // Not a Metrics endpoint, let it pass
            }

            _logger.LogDebug("Metrics endpoint accessed, validating API key and rate limit");

            // Check if API key is provided
            if (!context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out StringValues apiKeyHeader))
            {
                _logger.LogWarning("Metrics endpoint accessed without API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
                context.Result = new UnauthorizedObjectResult(new { error = "API key required for Metrics endpoints" });
                return;
            }

            var providedApiKey = apiKeyHeader.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(providedApiKey))
            {
                _logger.LogWarning("Metrics endpoint accessed with empty API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
                context.Result = new UnauthorizedObjectResult(new { error = "API key cannot be empty" });
                return;
            }

            // Get configured API key
            var configuredApiKey = _configuration["MetricsApiKey"];
            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                _logger.LogError("MetricsApiKey is not configured in application settings");
                context.Result = new UnauthorizedObjectResult(new { error = "API key validation not configured" });
                return;
            }

            // Validate API key using constant-time comparison
            if (!SecureEquals(providedApiKey, configuredApiKey))
            {
                _logger.LogWarning("Metrics endpoint accessed with invalid API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid API key" });              
            }
        }

        private static string? GetClientIpAddress(HttpContext context)
        {
            // Check for forwarded IP first (in case of proxy/load balancer)
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                return forwardedFor.FirstOrDefault()?.Split(',')[0].Trim();
            }

            if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                return realIp.FirstOrDefault();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks
        /// </summary>
        private static bool SecureEquals(string a, string b)
        {
            var abytes = Encoding.UTF8.GetBytes(a);
            var bbytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(abytes, bbytes);
        }
    }
}
