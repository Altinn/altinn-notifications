using System.Diagnostics;

using Altinn.Notifications.Telemetry;

using Microsoft.AspNetCore.Http;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingTelemetry;

public class RequestFilterProcessorTests
{
    private const string RequestOperationName = "Microsoft.AspNetCore.Hosting.HttpRequestIn";

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/")]
    [InlineData("/HEALTH")]
    [InlineData("/health/live")]
    [InlineData("/health/live/")]
    [InlineData("/health/ready")]
    [InlineData("/Health/Ready")]
    [InlineData("/notifications/api/v1/health/ready")]
    public void OnStart_HealthPath_SuppressesRequest(string path)
    {
        var processor = CreateProcessor(path);
        using var activity = CreateRecordedActivity(RequestOperationName);

        processor.OnStart(activity);

        Assert.Equal(0, (int)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded));
    }

    [Theory]
    [InlineData("/notifications/api/v1/orders")]
    [InlineData("/health/other")]
    [InlineData("/healthy")]
    public void OnStart_OtherPath_KeepsRequestRecorded(string path)
    {
        var processor = CreateProcessor(path);
        using var activity = CreateRecordedActivity(RequestOperationName);

        processor.OnStart(activity);

        Assert.NotEqual(0, (int)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded));
    }

    [Fact]
    public void OnStart_RequestWithoutHttpContext_KeepsRequestRecorded()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var processor = new RequestFilterProcessor(accessor.Object);
        using var activity = CreateRecordedActivity(RequestOperationName);

        processor.OnStart(activity);

        Assert.NotEqual(0, (int)(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded));
    }

    [Fact]
    public void OnStart_ChildOfUnrecordedParent_SuppressesActivity()
    {
        var processor = CreateProcessor("/notifications/api/v1/orders");
        using var parent = new Activity("parent").Start();
        parent.ActivityTraceFlags = ActivityTraceFlags.None;
        using var child = CreateRecordedActivity("child");
        child.SetParentId(parent.TraceId, parent.SpanId, ActivityTraceFlags.None);

        processor.OnStart(child);

        Assert.Equal(0, (int)(child.ActivityTraceFlags & ActivityTraceFlags.Recorded));
    }

    private static RequestFilterProcessor CreateProcessor(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);

        return new RequestFilterProcessor(accessor.Object);
    }

    private static Activity CreateRecordedActivity(string operationName)
    {
        var activity = new Activity(operationName).Start();
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        return activity;
    }
}
