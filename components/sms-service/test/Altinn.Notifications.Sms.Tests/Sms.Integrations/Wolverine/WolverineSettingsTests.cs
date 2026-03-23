using Altinn.Notifications.Sms.Integrations.Configuration;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations.Wolverine;

public class WolverineSettingsTests
{
    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.False(settings.EnableWolverine);
        Assert.Equal(10, settings.ListenerCount);
        Assert.NotNull(settings.SmsSendQueuePolicy);
        Assert.Equal(string.Empty, settings.SmsSendQueueName);
        Assert.False(settings.AcceptSmsNotificationsViaWolverine);
        Assert.Equal(string.Empty, settings.ServiceBusConnectionString);
    }

    [Fact]
    public void WolverineSettings_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:ListenerCount"] = "5",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:AcceptSmsNotificationsViaWolverine"] = "true",
                ["WolverineSettings:SmsSendQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:SmsSendQueuePolicy:CooldownDelaysMs:1"] = "5000",
                ["WolverineSettings:SmsSendQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:SmsSendQueueName"] = "altinn.notifications.sms.send",
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.EnableWolverine);

        Assert.Equal(5, settings.ListenerCount);

        Assert.True(settings.AcceptSmsNotificationsViaWolverine);
        Assert.Equal("altinn.notifications.sms.send", settings.SmsSendQueueName);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.SmsSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(5000), settings.SmsSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.SmsSendQueuePolicy.GetScheduleDelays());
    }
}
