using Altinn.Notifications.Shared.Configuration;
using Xunit;

namespace Altinn.Notifications.Shared.Tests.Configuration;

public class WolverineSettingsTests
{
    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.False(settings.EnableServiceBus);
        Assert.Equal(string.Empty, settings.ServiceBusConnectionString);
        Assert.Equal(10, settings.ListenerCount);
    }

    [Fact]
    public void WolverineSettings_QueuePolicies_DefaultToEmptyDelays()
    {
        var settings = new WolverineSettings();

        Assert.Empty(settings.EmailDeliveryReportQueuePolicy.GetCooldownDelays());
        Assert.Empty(settings.SmsDeliveryReportQueuePolicy.GetCooldownDelays());
    }
}
