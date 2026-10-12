using System.Diagnostics;

using Altinn.Notifications.Shared.Telemetry;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Telemetry;

public class WolverineActivityNameProcessorTests
{
    [Fact]
    public void OnEnd_ConsumerReceiveActivity_AppendsMessageTypeToDisplayName()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activitySource = new ActivitySource("test-source");
        using Activity? activity = activitySource.StartActivity("ASB receive", ActivityKind.Consumer);
        Assert.NotNull(activity);
        activity.SetTag("messaging.message_type", "MyMessage");

        var sut = new WolverineActivityNameProcessor();

        // Act
        sut.OnEnd(activity);

        // Assert
        Assert.Equal("ASB receive MyMessage", activity.DisplayName);
    }

    [Fact]
    public void OnEnd_ConsumerReceiveActivity_WithDestinationAndMessageType_PrefersDestinationQueueName()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activitySource = new ActivitySource("test-source");
        using Activity? activity = activitySource.StartActivity("ASB receive", ActivityKind.Consumer);
        Assert.NotNull(activity);
        activity.SetTag("messaging.message_type", "MyMessage");
        activity.SetTag("messaging.destination", new Uri("sb://namespace.servicebus.windows.net/my-queue"));

        var sut = new WolverineActivityNameProcessor();

        // Act
        sut.OnEnd(activity);

        // Assert
        Assert.Equal("ASB receive my-queue", activity.DisplayName);
    }

    [Fact]
    public void OnEnd_ConsumerReceiveActivity_WithDestinationAsStringUri_UsesQueueName()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activitySource = new ActivitySource("test-source");
        using Activity? activity = activitySource.StartActivity("ASB receive", ActivityKind.Consumer);
        Assert.NotNull(activity);
        activity.SetTag("messaging.destination.name", "sb://namespace.servicebus.windows.net/queue-name");

        var sut = new WolverineActivityNameProcessor();

        // Act
        sut.OnEnd(activity);

        // Assert
        Assert.Equal("ASB receive queue-name", activity.DisplayName);
    }

    [Fact]
    public void OnEnd_ProducerSendActivity_WithExistingMessageTypeInName_DoesNotChangeDisplayName()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activitySource = new ActivitySource("test-source");
        using Activity? activity = activitySource.StartActivity("ASB send MyMessage", ActivityKind.Producer);
        Assert.NotNull(activity);
        activity.SetTag("messaging.message_type", "MyMessage");

        var sut = new WolverineActivityNameProcessor();

        // Act
        sut.OnEnd(activity);

        // Assert
        Assert.Equal("ASB send MyMessage", activity.DisplayName);
    }

    [Fact]
    public void OnEnd_InternalActivity_DoesNotChangeDisplayName()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activitySource = new ActivitySource("test-source");
        using Activity? activity = activitySource.StartActivity("custom operation", ActivityKind.Internal);
        Assert.NotNull(activity);
        activity.SetTag("messaging.message_type", "MyMessage");

        var sut = new WolverineActivityNameProcessor();

        // Act
        sut.OnEnd(activity);

        // Assert
        Assert.Equal("custom operation", activity.DisplayName);
    }
}
