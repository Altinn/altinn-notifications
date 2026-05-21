using Altinn.Notifications.Shared.Configuration;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Configuration;

public class WolverineSettingsTests
{
    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettingsBase();

        Assert.True(settings.EnableWolverine);
        Assert.Equal(string.Empty, settings.ServiceBusConnectionString);
    }

    [Fact]
    public void QueueRetryPolicy_DefaultsToEmptyDelays()
    {
        var policy = new QueueRetryPolicy();

        Assert.Empty(policy.GetCooldownDelays());
        Assert.Empty(policy.GetScheduleDelays());
    }
}
