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
        Assert.Equal(string.Empty, settings.EmailSendQueueName);
        Assert.False(settings.AcceptEmailNotificationsViaWolverine);
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
                ["WolverineSettings:AcceptEmailNotificationsViaWolverine"] = "true",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:1"] = "5000",
                ["WolverineSettings:EmailSendQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.EnableWolverine);

        Assert.Equal(5, settings.ListenerCount);

        Assert.True(settings.AcceptEmailNotificationsViaWolverine);
        Assert.Equal("altinn.notifications.email.send", settings.EmailSendQueueName);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(5000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailSendQueuePolicy.GetScheduleDelays());
    }
}
