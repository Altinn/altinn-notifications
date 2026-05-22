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

        Assert.True(settings.EnableWolverine);
        Assert.Equal(string.Empty, settings.ServiceBusConnectionString);

        Assert.Equal(10, settings.EmailSendListenerCount);
        Assert.NotNull(settings.EmailSendQueuePolicy);
        Assert.True(settings.EnableSendEmailListener);
        Assert.Equal(string.Empty, settings.EmailSendQueueName);

        Assert.Equal(10, settings.EmailStatusCheckListenerCount);
        Assert.NotNull(settings.EmailStatusCheckQueuePolicy);
        Assert.True(settings.EnableEmailStatusCheckListener);
        Assert.True(settings.EnableEmailStatusCheckPublisher);
        Assert.Equal(string.Empty, settings.EmailStatusCheckQueueName);

        Assert.True(settings.EnableEmailSendResultPublisher);
        Assert.Equal(string.Empty, settings.EmailSendResultQueueName);

        Assert.True(settings.EnableEmailServiceRateLimitPublisher);
        Assert.Equal(string.Empty, settings.EmailServiceRateLimitQueueName);
    }

    [Fact]
    public void WolverineSettings_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:EmailSendListenerCount"] = "5",
                ["WolverineSettings:EmailStatusCheckListenerCount"] = "3",
                ["WolverineSettings:EnableWolverine"] = "true",
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",
                ["WolverineSettings:EnableSendEmailListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckListener"] = "true",
                ["WolverineSettings:EnableEmailStatusCheckPublisher"] = "true",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailSendQueuePolicy:CooldownDelaysMs:1"] = "5000",
                ["WolverineSettings:EmailSendQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",
                ["WolverineSettings:EmailStatusCheckQueuePolicy:CooldownDelaysMs:0"] = "500",
                ["WolverineSettings:EmailStatusCheckQueuePolicy:ScheduleDelaysMs:0"] = "30000",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
                ["WolverineSettings:EnableEmailSendResultPublisher"] = "true",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EnableEmailServiceRateLimitPublisher"] = "true",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.EnableWolverine);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);

        Assert.Equal(5, settings.EmailSendListenerCount);
        Assert.True(settings.EnableSendEmailListener);
        Assert.Equal("altinn.notifications.email.send", settings.EmailSendQueueName);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(5000), settings.EmailSendQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailSendQueuePolicy.GetScheduleDelays());

        Assert.Equal(3, settings.EmailStatusCheckListenerCount);
        Assert.True(settings.EnableEmailStatusCheckListener);
        Assert.True(settings.EnableEmailStatusCheckPublisher);
        Assert.Equal("altinn.notifications.email.check.send.status", settings.EmailStatusCheckQueueName);
        Assert.Contains(TimeSpan.FromMilliseconds(500), settings.EmailStatusCheckQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(30000), settings.EmailStatusCheckQueuePolicy.GetScheduleDelays());

        Assert.True(settings.EnableEmailSendResultPublisher);
        Assert.Equal("altinn.notifications.email.send.result", settings.EmailSendResultQueueName);

        Assert.True(settings.EnableEmailServiceRateLimitPublisher);
        Assert.Equal("altinn.notifications.email.send.ratelimit", settings.EmailServiceRateLimitQueueName);
    }
}
