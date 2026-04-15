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

        Assert.False(settings.EnableWolverine);
        Assert.Equal(10, settings.ListenerCount);
        
        Assert.NotNull(settings.EmailSendQueuePolicy);
        Assert.False(settings.EnableSendEmailListener);
        Assert.Equal(string.Empty, settings.EmailSendQueueName);

        Assert.NotNull(settings.EmailStatusCheckQueuePolicy);
        Assert.False(settings.EnableEmailStatusCheckListener);
        Assert.False(settings.EnableEmailStatusCheckPublisher);
        Assert.Equal(string.Empty, settings.EmailStatusCheckQueueName);

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
                ["WolverineSettings:EnableSendEmailListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:1"] = "5000",
                ["WolverineSettings:EmailSendQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",
                ["WolverineSettings:EmailStatusCheckQueuePolicy:CooldownDelaysMs:0"] = "500",
                ["WolverineSettings:EmailStatusCheckQueuePolicy:ScheduleDelaysMs:0"] = "30000",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check-send-status",
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.EnableWolverine);

        Assert.Equal(5, settings.ListenerCount);

        Assert.True(settings.EnableSendEmailListener);
        Assert.Equal("altinn.notifications.email.send", settings.EmailSendQueueName);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(5000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailSendQueuePolicy.GetScheduleDelays());

        Assert.True(settings.EnableEmailStatusCheckListener);
        Assert.Equal("altinn.notifications.email.check-send-status", settings.EmailStatusCheckQueueName);
        Assert.Contains(TimeSpan.FromMilliseconds(500), settings.EmailStatusCheckQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(30000), settings.EmailStatusCheckQueuePolicy.GetScheduleDelays());
    }
}
