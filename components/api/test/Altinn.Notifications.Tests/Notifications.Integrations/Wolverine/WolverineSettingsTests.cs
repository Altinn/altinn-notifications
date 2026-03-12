using System;
using System.Collections.Generic;

using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class WolverineSettingsTests
{
    [Fact]
    public void WolverineSettings_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:EnableEmailDeliveryReportListener"] = "true",
                ["WolverineSettings:EmailDeliveryReportQueueName"] = "altinn.notifications.email.deliveryreports",
                ["WolverineSettings:EmailDeliveryReportQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailDeliveryReportQueuePolicy:ScheduleDelaysMs:0"] = "60000"
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.EnableWolverine);
        Assert.True(settings.EnableEmailDeliveryReportListener);
        Assert.Equal("altinn.notifications.email.deliveryreports", settings.EmailDeliveryReportQueueName);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailDeliveryReportQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailDeliveryReportQueuePolicy.GetScheduleDelays());
    }

    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.False(settings.EnableWolverine);
        Assert.False(settings.EnableEmailDeliveryReportListener);
        Assert.Equal(string.Empty, settings.EmailDeliveryReportQueueName);
        Assert.NotNull(settings.EmailDeliveryReportQueuePolicy);
    }
}
