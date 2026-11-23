using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class WolverineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWolverineServices_RegistersDependencyHealthChecksForListenerAndEachListenQueue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://127.0.0.1;SharedAccessKeyName=key;SharedAccessKey=value;UseDevelopmentEmulator=true;",
                ["WolverineSettings:SendSmsQueueName"] = "sms.send",
                ["WolverineSettings:EmailSendQueueName"] = "email.send",
                ["WolverineSettings:ComposedEmailSendQueueName"] = "composedemail.send",
                ["WolverineSettings:SmsSendResultQueueName"] = "sms.send.result",
                ["WolverineSettings:EmailSendResultQueueName"] = "email.send.result",
                ["WolverineSettings:SmsDeliveryReportQueueName"] = "sms.deliveryreports",
                ["WolverineSettings:EmailDeliveryReportQueueName"] = "email.deliveryreports",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "email.send.ratelimit"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddWolverineServices(configuration, Mock.Of<IHostEnvironment>());

        using ServiceProvider provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        string[] expectedNames =
        [
            "notifications_wolverine_health_check",
            "notifications_service_bus_sms.send.result_health_check",
            "notifications_service_bus_email.send.result_health_check",
            "notifications_service_bus_sms.deliveryreports_health_check",
            "notifications_service_bus_email.deliveryreports_health_check",
            "notifications_service_bus_email.send.ratelimit_health_check"
        ];

        Assert.Equal(expectedNames.Order(), registrations.Select(r => r.Name).Order());
        Assert.All(registrations, r => Assert.Equal([HealthCheckTags.Dependency], r.Tags));
        Assert.All(registrations, r => Assert.Equal(TimeSpan.FromSeconds(5), r.Timeout));
    }
}
