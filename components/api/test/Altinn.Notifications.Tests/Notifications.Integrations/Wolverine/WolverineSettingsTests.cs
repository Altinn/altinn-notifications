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
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/",

                ["WolverineSettings:EmailPublishConcurrency"] = "5",
                ["WolverineSettings:EnableSendEmailPublisher"] = "true",
                ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",

                ["WolverineSettings:SmsPublishConcurrency"] = "3",
                ["WolverineSettings:EnableSendSmsPublisher"] = "true",
                ["WolverineSettings:SendSmsQueueName"] = "altinn.notifications.sms.send",

                ["WolverineSettings:EnableEmailDeliveryReportListener"] = "true",
                ["WolverineSettings:EmailDeliveryReportQueueName"] = "altinn.notifications.email.deliveryreports",
                ["WolverineSettings:EmailDeliveryReportListenerCount"] = "3",
                ["WolverineSettings:EmailDeliveryReportQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailDeliveryReportQueuePolicy:ScheduleDelaysMs:0"] = "60000",

                ["WolverineSettings:EnableSmsDeliveryReportListener"] = "true",
                ["WolverineSettings:SmsDeliveryReportQueueName"] = "altinn.notifications.sms.deliveryreports",
                ["WolverineSettings:SmsDeliveryReportListenerCount"] = "4",
                ["WolverineSettings:SmsDeliveryReportQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:SmsDeliveryReportQueuePolicy:ScheduleDelaysMs:0"] = "60000",

                ["WolverineSettings:EnableEmailSendResultListener"] = "true",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["WolverineSettings:EmailSendResultListenerCount"] = "5",
                ["WolverineSettings:EmailSendResultQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:EmailSendResultQueuePolicy:ScheduleDelaysMs:0"] = "60000",

                ["WolverineSettings:EnableSmsSendResultListener"] = "true",
                ["WolverineSettings:SmsSendResultQueueName"] = "altinn.notifications.sms.send.result",
                ["WolverineSettings:SmsSendResultListenerCount"] = "6",
                ["WolverineSettings:SmsSendResultQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:SmsSendResultQueuePolicy:ScheduleDelaysMs:0"] = "60000",

                ["WolverineSettings:EnableEmailServiceRateLimitListener"] = "true",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit",
                ["WolverineSettings:EmailServiceRateLimitListenerCount"] = "2",
                ["WolverineSettings:EmailServiceRateLimitQueuePolicy:CooldownDelaysMs:0"] = "500",
                ["WolverineSettings:EmailServiceRateLimitQueuePolicy:ScheduleDelaysMs:0"] = "30000",

                ["WolverineSettings:EnablePastDueOrderPublisher"] = "true",
                ["WolverineSettings:EnablePastDueOrderListener"] = "true",
                ["WolverineSettings:PastDueOrdersPublishConcurrency"] = "8",
                ["WolverineSettings:PastDueOrdersQueueName"] = "altinn.notifications.orders.pastdue",
                ["WolverineSettings:PastDueOrdersListenerCount"] = "7",
                ["WolverineSettings:PastDueOrdersRetryDelayMs"] = "30000",
                ["WolverineSettings:PastDueOrdersQueuePolicy:CooldownDelaysMs:0"] = "1000",
                ["WolverineSettings:PastDueOrdersQueuePolicy:ScheduleDelaysMs:0"] = "60000",
            })
            .Build();

        var settings = config.GetSection("WolverineSettings").Get<WolverineSettings>();

        Assert.NotNull(settings);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/", settings.ServiceBusConnectionString);

        Assert.Equal(5, settings.EmailPublishConcurrency);
        Assert.True(settings.EnableSendEmailPublisher);
        Assert.Equal("altinn.notifications.email.send", settings.EmailSendQueueName);

        Assert.Equal(3, settings.SmsPublishConcurrency);
        Assert.True(settings.EnableSendSmsPublisher);
        Assert.Equal("altinn.notifications.sms.send", settings.SendSmsQueueName);

        Assert.True(settings.EnableEmailDeliveryReportListener);
        Assert.Equal("altinn.notifications.email.deliveryreports", settings.EmailDeliveryReportQueueName);
        Assert.Equal(3, settings.EmailDeliveryReportListenerCount);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailDeliveryReportQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailDeliveryReportQueuePolicy.GetScheduleDelays());

        Assert.True(settings.EnableSmsDeliveryReportListener);
        Assert.Equal("altinn.notifications.sms.deliveryreports", settings.SmsDeliveryReportQueueName);
        Assert.Equal(4, settings.SmsDeliveryReportListenerCount);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.SmsDeliveryReportQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.SmsDeliveryReportQueuePolicy.GetScheduleDelays());

        Assert.True(settings.EnableEmailSendResultListener);
        Assert.Equal("altinn.notifications.email.send.result", settings.EmailSendResultQueueName);
        Assert.Equal(5, settings.EmailSendResultListenerCount);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.EmailSendResultQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.EmailSendResultQueuePolicy.GetScheduleDelays());

        Assert.True(settings.EnableSmsSendResultListener);
        Assert.Equal("altinn.notifications.sms.send.result", settings.SmsSendResultQueueName);
        Assert.Equal(6, settings.SmsSendResultListenerCount);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.SmsSendResultQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.SmsSendResultQueuePolicy.GetScheduleDelays());

        Assert.True(settings.EnableEmailServiceRateLimitListener);
        Assert.Equal("altinn.notifications.email.send.ratelimit", settings.EmailServiceRateLimitQueueName);
        Assert.Equal(2, settings.EmailServiceRateLimitListenerCount);
        Assert.Contains(TimeSpan.FromMilliseconds(500), settings.EmailServiceRateLimitQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(30000), settings.EmailServiceRateLimitQueuePolicy.GetScheduleDelays());

        Assert.True(settings.EnablePastDueOrderPublisher);
        Assert.True(settings.EnablePastDueOrderListener);
        Assert.Equal(8, settings.PastDueOrdersPublishConcurrency);
        Assert.Equal("altinn.notifications.orders.pastdue", settings.PastDueOrdersQueueName);
        Assert.Equal(7, settings.PastDueOrdersListenerCount);
        Assert.Equal(30000, settings.PastDueOrdersRetryDelayMs);
        Assert.Contains(TimeSpan.FromMilliseconds(1000), settings.PastDueOrdersQueuePolicy.GetCooldownDelays());
        Assert.Contains(TimeSpan.FromMilliseconds(60000), settings.PastDueOrdersQueuePolicy.GetScheduleDelays());
    }

    [Fact]
    public void WolverineSettings_HasExpectedDefaults()
    {
        var settings = new WolverineSettings();

        Assert.Equal(string.Empty, settings.ServiceBusConnectionString);

        Assert.Equal(10, settings.EmailPublishConcurrency);
        Assert.True(settings.EnableSendEmailPublisher);
        Assert.Equal(string.Empty, settings.EmailSendQueueName);

        Assert.Equal(10, settings.SmsPublishConcurrency);
        Assert.True(settings.EnableSendSmsPublisher);
        Assert.Equal(string.Empty, settings.SendSmsQueueName);

        Assert.NotNull(settings.EmailDeliveryReportQueuePolicy);
        Assert.True(settings.EnableEmailDeliveryReportListener);
        Assert.Equal(string.Empty, settings.EmailDeliveryReportQueueName);
        Assert.Equal(10, settings.EmailDeliveryReportListenerCount);

        Assert.NotNull(settings.SmsDeliveryReportQueuePolicy);
        Assert.True(settings.EnableSmsDeliveryReportListener);
        Assert.Equal(string.Empty, settings.SmsDeliveryReportQueueName);
        Assert.Equal(10, settings.SmsDeliveryReportListenerCount);

        Assert.NotNull(settings.EmailSendResultQueuePolicy);
        Assert.True(settings.EnableEmailSendResultListener);
        Assert.Equal(string.Empty, settings.EmailSendResultQueueName);
        Assert.Equal(10, settings.EmailSendResultListenerCount);

        Assert.NotNull(settings.SmsSendResultQueuePolicy);
        Assert.True(settings.EnableSmsSendResultListener);
        Assert.Equal(string.Empty, settings.SmsSendResultQueueName);
        Assert.Equal(10, settings.SmsSendResultListenerCount);

        Assert.NotNull(settings.EmailServiceRateLimitQueuePolicy);
        Assert.True(settings.EnableEmailServiceRateLimitListener);
        Assert.Equal(string.Empty, settings.EmailServiceRateLimitQueueName);
        Assert.Equal(1, settings.EmailServiceRateLimitListenerCount);

        Assert.NotNull(settings.PastDueOrdersQueuePolicy);
        Assert.True(settings.EnablePastDueOrderPublisher);
        Assert.True(settings.EnablePastDueOrderListener);
        Assert.Equal(10, settings.PastDueOrdersPublishConcurrency);
        Assert.Equal(string.Empty, settings.PastDueOrdersQueueName);
        Assert.Equal(10, settings.PastDueOrdersListenerCount);
        Assert.Equal(60_000, settings.PastDueOrdersRetryDelayMs);
    }
}
