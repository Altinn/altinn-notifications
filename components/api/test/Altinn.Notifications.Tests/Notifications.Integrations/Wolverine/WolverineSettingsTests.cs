using Altinn.Notifications.Integrations.Configuration;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class WolverineSettingsTests
{
    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.False(settings.EnableEmailDeliveryReportListener);
        Assert.Equal(string.Empty, settings.EmailDeliveryReportQueueName);
        Assert.NotNull(settings.EmailDeliveryReportQueuePolicy);
    }
}
