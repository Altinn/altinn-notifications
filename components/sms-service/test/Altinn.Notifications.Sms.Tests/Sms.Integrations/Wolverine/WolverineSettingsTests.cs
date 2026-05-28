using Altinn.Notifications.Sms.Integrations.Configuration;

using Microsoft.Extensions.Configuration;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations.Wolverine;

public class WolverineSettingsTests
{
    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.Equal(string.Empty, settings.ServiceBusConnectionString);

        Assert.Equal(10, settings.SendSmsListenerCount);
        Assert.NotNull(settings.SendSmsQueuePolicy);
        Assert.True(settings.EnableSendSmsListener);
        Assert.Equal(string.Empty, settings.SendSmsQueueName);

        Assert.Equal(string.Empty, settings.SmsDeliveryReportQueueName);
        Assert.Equal(string.Empty, settings.SmsSendResultQueueName);
    }

    [Fact]
    public void WolverineSettings_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:SendSmsListenerCount"] = "5",
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",
                ["WolverineSettings:EnableSendSmsListener"] = "true",
                ["WolverineSettings:SendSmsQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:SendSmsQueuePolicy:CooldownDelaysMs:1"] = "5000",
                ["WolverineSettings:SendSmsQueuePolicy:ScheduleDelaysMs:0"] = "60000",
                ["WolverineSettings:SendSmsQueueName"] = "altinn.notifications.sms.send",
                ["WolverineSettings:SmsDeliveryReportQueueName"] = "altinn.notifications.sms.deliveryreports",
                ["WolverineSettings:SmsSendResultQueueName"] = "altinn.notifications.sms.send.result",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);

        Assert.Equal(5, settings.SendSmsListenerCount);
        Assert.True(settings.EnableSendSmsListener);
        Assert.Equal("altinn.notifications.sms.send", settings.SendSmsQueueName);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.SendSmsQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(5000), settings.SendSmsQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.SendSmsQueuePolicy.GetScheduleDelays());

        Assert.Equal("altinn.notifications.sms.deliveryreports", settings.SmsDeliveryReportQueueName);
        Assert.Equal("altinn.notifications.sms.send.result", settings.SmsSendResultQueueName);
    }
}
