using Altinn.Notifications.Email.Integrations.Configuration;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations.Wolverine;

public class WolverineSettingsTests
{
    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.Equal(string.Empty, settings.ServiceBusConnectionString);

        Assert.Equal(string.Empty, settings.EmailSendQueueName);
        Assert.Equal(10, settings.EmailSendListenerCount);
        Assert.NotNull(settings.EmailSendQueuePolicy);

        Assert.Equal(string.Empty, settings.EmailStatusCheckQueueName);
        Assert.Equal(10, settings.EmailStatusCheckListenerCount);
        Assert.NotNull(settings.EmailStatusCheckQueuePolicy);

        Assert.Equal(string.Empty, settings.EmailSendResultQueueName);
        Assert.Equal(string.Empty, settings.EmailServiceRateLimitQueueName);
    }

    [Fact]
    public void WolverineSettings_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",
                ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",
                ["WolverineSettings:EmailSendListenerCount"] = "5",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:1"] = "5000",
                ["WolverineSettings:EmailSendQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
                ["WolverineSettings:EmailStatusCheckListenerCount"] = "3",
                ["WolverineSettings:EmailStatusCheckQueuePolicy:CooldownDelaysMs:0"] = "500",
                ["WolverineSettings:EmailStatusCheckQueuePolicy:ScheduleDelaysMs:0"] = "30000",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);

        Assert.Equal("altinn.notifications.email.send", settings.EmailSendQueueName);
        Assert.Equal(5, settings.EmailSendListenerCount);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(5000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailSendQueuePolicy.GetScheduleDelays());

        Assert.Equal("altinn.notifications.email.check.send.status", settings.EmailStatusCheckQueueName);
        Assert.Equal(3, settings.EmailStatusCheckListenerCount);
        Assert.Contains(TimeSpan.FromMilliseconds(500), settings.EmailStatusCheckQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(30000), settings.EmailStatusCheckQueuePolicy.GetScheduleDelays());

        Assert.Equal("altinn.notifications.email.send.result", settings.EmailSendResultQueueName);
        Assert.Equal("altinn.notifications.email.send.ratelimit", settings.EmailServiceRateLimitQueueName);
    }
}
