using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Notifications.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Authorization;

public class MetricsApiKeyFilterTests
{
    private static AuthorizationFilterContext CreateAuthorizationContext(string path, IHeaderDictionary? headers = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                httpContext.Request.Headers[kvp.Key] = kvp.Value;
            }
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public async Task OnAuthorizationAsync_NonMetricsPath_DoesNotSetResult()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MetricsApiKey"] = "key" }).Build();
        var logger = Mock.Of<ILogger<MetricsApiKeyFilter>>();
        var filter = new MetricsApiKeyFilter(config, logger);

        var context = CreateAuthorizationContext("/health");

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_MetricsPath_MissingApiKeyHeader_ReturnsUnauthorized()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MetricsApiKey"] = "key" }).Build();
        var logger = Mock.Of<ILogger<MetricsApiKeyFilter>>();
        var filter = new MetricsApiKeyFilter(config, logger);

        var context = CreateAuthorizationContext("/notifications/api/v1/metrics/sms");

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        var errorValue = result.Value?.GetType().GetProperty("error")?.GetValue(result.Value)?.ToString();
        Assert.Equal("API key required for Metrics endpoints", errorValue);
    }

    [Fact]
    public async Task OnAuthorizationAsync_MetricsPath_EmptyApiKeyHeader_ReturnsUnauthorized()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MetricsApiKey"] = "key" }).Build();
        var logger = Mock.Of<ILogger<MetricsApiKeyFilter>>();
        var filter = new MetricsApiKeyFilter(config, logger);

        var headers = new HeaderDictionary { ["X-API-Key"] = StringValues.Empty };
        var context = CreateAuthorizationContext("/notifications/api/v1/metrics/sms", headers);

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        var errorValue = result.Value?.GetType().GetProperty("error")?.GetValue(result.Value)?.ToString();
        Assert.Equal("API key required for Metrics endpoints", errorValue);
    }

    [Fact]
    public async Task OnAuthorizationAsync_MetricsPath_ConfiguredApiKeyMissing_ReturnsUnauthorized()
    {
        // Arrange - no MetricsApiKey configured
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var logger = Mock.Of<ILogger<MetricsApiKeyFilter>>();
        var filter = new MetricsApiKeyFilter(config, logger);

        var headers = new HeaderDictionary { ["X-API-Key"] = "provided" };
        var context = CreateAuthorizationContext("/notifications/api/v1/metrics/sms", headers);

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        var errorValue = result.Value?.GetType().GetProperty("error")?.GetValue(result.Value)?.ToString();
        Assert.Equal("API key validation not configured", errorValue);
    }

    [Fact]
    public async Task OnAuthorizationAsync_MetricsPath_InvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MetricsApiKey"] = "expected" }).Build();
        var logger = Mock.Of<ILogger<MetricsApiKeyFilter>>();
        var filter = new MetricsApiKeyFilter(config, logger);

        var headers = new HeaderDictionary { ["X-API-Key"] = "invalid" };
        var context = CreateAuthorizationContext("/notifications/api/v1/metrics/sms", headers);

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        var errorValue = result.Value?.GetType().GetProperty("error")?.GetValue(result.Value)?.ToString();
        Assert.Equal("Invalid API key", errorValue);
    }

    [Fact]
    public async Task OnAuthorizationAsync_MetricsPath_ValidApiKey_AllowsRequest()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MetricsApiKey"] = "expected" }).Build();
        var logger = Mock.Of<ILogger<MetricsApiKeyFilter>>();
        var filter = new MetricsApiKeyFilter(config, logger);

        var headers = new HeaderDictionary { ["X-API-Key"] = "expected" };
        var context = CreateAuthorizationContext("/notifications/api/v1/metrics/sms", headers);

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        Assert.Null(context.Result);
    }
}
