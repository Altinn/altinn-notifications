using System.Diagnostics;

using Altinn.Notifications.Shared.Wolverine.Middleware;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Wolverine;

public class WolverineHandlerTimingMiddlewareTests
{
    [Fact]
    public void After_WithCurrentActivityAndStartHeader_SetsDurationTags()
    {
        using var activity = new Activity("test");
        activity.Start();

        Envelope envelope = new(new object());
        WolverineHandlerTimingMiddleware.Before(envelope);

        WolverineHandlerTimingMiddleware.After(envelope);

        Assert.NotNull(activity.GetTagItem("handle.duration.ms"));
    }

    [Fact]
    public void After_WithoutStartHeader_DoesNotSetDurationTags()
    {
        using var activity = new Activity("test");
        activity.Start();

        Envelope envelope = new(new object());
        WolverineHandlerTimingMiddleware.After(envelope);

        Assert.Null(activity.GetTagItem("handle.duration.ms"));
    }

    [Fact]
    public void After_WithoutCurrentActivity_CompletesWithoutThrowing()
    {
        Envelope envelope = new(new object());
        WolverineHandlerTimingMiddleware.Before(envelope);

        WolverineHandlerTimingMiddleware.After(envelope);

        Assert.True(true);
    }
}
