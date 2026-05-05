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
                ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",

                ["WolverineSettings:EnableEmailDeliveryReportListener"] = "true",
                ["WolverineSettings:EmailDeliveryReportQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailDeliveryReportQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:EmailDeliveryReportQueueName"] = "altinn.notifications.email.deliveryreports",
                ["WolverineSettings:EmailDeliveryReportListenerCount"] = "3",

                ["WolverineSettings:SmsDeliveryReportListenerCount"] = "4",
                ["WolverineSettings:EmailSendResultListenerCount"] = "5",
                ["WolverineSettings:SmsSendResultListenerCount"] = "6",
                ["WolverineSettings:EmailServiceRateLimitListenerCount"] = "2",
                ["WolverineSettings:PastDueOrdersListenerCount"] = "7",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);

        Assert.True(settings.EnableWolverine);

        Assert.Equal("altinn.notifications.email.send", settings.EmailSendQueueName);

        Assert.True(settings.EnableEmailDeliveryReportListener);
        Assert.Equal("altinn.notifications.email.deliveryreports", settings.EmailDeliveryReportQueueName);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailDeliveryReportQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailDeliveryReportQueuePolicy.GetScheduleDelays());
        Assert.Equal(3, settings.EmailDeliveryReportListenerCount);

        Assert.Equal(4, settings.SmsDeliveryReportListenerCount);
        Assert.Equal(5, settings.EmailSendResultListenerCount);
        Assert.Equal(6, settings.SmsSendResultListenerCount);
        Assert.Equal(2, settings.EmailServiceRateLimitListenerCount);
        Assert.Equal(7, settings.PastDueOrdersListenerCount);
    }

    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.False(settings.EnableWolverine);

        Assert.Equal(string.Empty, settings.EmailSendQueueName);

        Assert.NotNull(settings.EmailDeliveryReportQueuePolicy);
        Assert.False(settings.EnableEmailDeliveryReportListener);
        Assert.Equal(string.Empty, settings.EmailDeliveryReportQueueName);

        Assert.Equal(10, settings.EmailSendResultListenerCount);
        Assert.Equal(10, settings.SmsSendResultListenerCount);
        Assert.Equal(10, settings.EmailDeliveryReportListenerCount);
        Assert.Equal(10, settings.SmsDeliveryReportListenerCount);
        Assert.Equal(10, settings.PastDueOrdersListenerCount);
        Assert.Equal(1, settings.EmailServiceRateLimitListenerCount);
    }
}
