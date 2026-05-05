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
        Assert.NotNull(settings.SendSmsQueuePolicy);
        Assert.Equal(string.Empty, settings.SendSmsQueueName);
        Assert.False(settings.EnableSendSmsListener);
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
                ["WolverineSettings:EnableSendSmsListener"] = "true",
                ["WolverineSettings:SendSmsQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:SendSmsQueuePolicy:CooldownDelaysMs:1"] = "5000",
                ["WolverineSettings:SendSmsQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:SendSmsQueueName"] = "altinn.notifications.sms.send",
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.EnableWolverine);

        Assert.Equal(5, settings.ListenerCount);

        Assert.True(settings.EnableSendSmsListener);
        Assert.Equal("altinn.notifications.sms.send", settings.SendSmsQueueName);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.SendSmsQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(5000), settings.SendSmsQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.SendSmsQueuePolicy.GetScheduleDelays());
    }
}
