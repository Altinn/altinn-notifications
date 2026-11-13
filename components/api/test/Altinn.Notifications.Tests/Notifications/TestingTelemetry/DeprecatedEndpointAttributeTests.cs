using System.Diagnostics.Metrics;
using System.Reflection;

using Altinn.Notifications.Telemetry;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingTelemetry;

public class DeprecatedEndpointAttributeTests
{
    [Fact]
    public void ObsoleteControllerActions_AreAllMarkedWithDeprecatedEndpoint()
    {
        // Arrange
        var actions = typeof(Program).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        // Act
        var obsoleteActions = actions.Where(m => m.GetCustomAttribute<ObsoleteAttribute>() != null).ToList();
        var missingAttribute = obsoleteActions
            .Where(m => m.GetCustomAttribute<DeprecatedEndpointAttribute>() == null)
            .Select(m => $"{m.DeclaringType?.Name}.{m.Name}")
            .ToList();

        var markedButNotObsolete = actions
            .Where(m => m.GetCustomAttribute<DeprecatedEndpointAttribute>() != null && m.GetCustomAttribute<ObsoleteAttribute>() == null)
            .Select(m => $"{m.DeclaringType?.Name}.{m.Name}")
            .ToList();

        // Assert
        Assert.Empty(missingAttribute);
        Assert.NotEmpty(obsoleteActions);
        Assert.Empty(markedButNotObsolete);
    }

    [Theory]
    [InlineData("POST", "notifications/api/v1/orders", "OrdersController.Post")]
    [InlineData("GET", "notifications/api/v1/orders/{id}", "OrdersController.GetById")]
    [InlineData("GET", "notifications/api/v1/orders", "OrdersController.GetBySendersRef")]
    [InlineData("GET", "notifications/api/v1/future/log", "NotificationLogController.Get")]
    [InlineData("PUT", "notifications/api/v1/orders/{id}/cancel", "OrdersController.CancelOrder")]
    [InlineData("POST", "notifications/api/v1/orders/sms", "SmsNotificationOrdersController.Post")]
    [InlineData("GET", "notifications/api/v1/orders/{id}/status", "OrdersController.GetStatusById")]
    [InlineData("POST", "notifications/api/v1/future/orders/instant", "InstantOrdersController.Post")]
    [InlineData("POST", "notifications/api/v1/orders/email", "EmailNotificationOrdersController.Post")]
    [InlineData("GET", "notifications/api/v1/orders/{id}/notifications/sms", "SmsNotificationsController.GetByOrderId")]
    [InlineData("GET", "notifications/api/v1/orders/{id}/notifications/email", "EmailNotificationsController.GetByOrderId")]
    public void OnActionExecuting_LogsWarningAndIncrementsCounterWithCreator(string httpMethod, string route, string endpoint)
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DeprecatedEndpointFilter>>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";
        httpContext.Request.Method = httpMethod;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor
            {
                DisplayName = endpoint,
                AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo { Template = route }
            });
        var context = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());

        var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == DeprecatedEndpointAttribute.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            measurements.Add((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        });
        listener.Start();

        // Act
        new DeprecatedEndpointFilter(loggerMock.Object).OnActionExecuting(context);

        // Assert
        var (value, tags) = Assert.Single(measurements);
        Assert.Equal(1, value);
        Assert.Equal("ttd", tags["creator"]);
        Assert.Equal(httpMethod, tags["method"]);
        Assert.Equal(route, tags["route"]);
        Assert.Equal(endpoint, tags["endpoint"]);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => $"{state}".Contains("DeprecatedEndpointCalled") && $"{state}".Contains("ttd")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
