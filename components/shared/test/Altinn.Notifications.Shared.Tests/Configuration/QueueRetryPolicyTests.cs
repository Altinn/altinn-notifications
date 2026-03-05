using Altinn.Notifications.Shared.Configuration;
using Xunit;

namespace Altinn.Notifications.Shared.Tests.Configuration;

public class QueueRetryPolicyTests
{
    [Fact]
    public void GetCooldownDelays_ConvertsMsToTimeSpan()
    {
        var policy = new QueueRetryPolicy { CooldownDelaysMs = [100, 500, 1000] };

        var result = policy.GetCooldownDelays();

        Assert.Equal(3, result.Length);
        Assert.Equal(TimeSpan.FromMilliseconds(100), result[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(500), result[1]);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), result[2]);
    }

    [Fact]
    public void GetScheduleDelays_ConvertsMsToTimeSpan()
    {
        var policy = new QueueRetryPolicy { ScheduleDelaysMs = [5000, 30000] };

        var result = policy.GetScheduleDelays();

        Assert.Equal(2, result.Length);
        Assert.Equal(TimeSpan.FromMilliseconds(5000), result[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(30000), result[1]);
    }

    [Fact]
    public void GetCooldownDelays_ReturnsEmpty_WhenNoDelaysConfigured()
    {
        var policy = new QueueRetryPolicy();

        Assert.Empty(policy.GetCooldownDelays());
        Assert.Empty(policy.GetScheduleDelays());
    }
}
