using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations.Wolverine;

public class WolverineServiceCollectionExtensionsTests
{
    private static IHostEnvironment CreateHostEnvironment()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");
        return env.Object;
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddWolverineServices_SmsDeliveryReportQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ==",
            ["WolverineSettings:EnableSendSmsListener"] = "true",
            ["WolverineSettings:SendSmsListenerCount"] = "1",
            ["WolverineSettings:SendSmsQueueName"] = "altinn.notifications.sms.send",
            ["WolverineSettings:SmsDeliveryReportQueueName"] = queueName,
        });

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddWolverineServices(config, CreateHostEnvironment()));

        // Assert
        Assert.Contains(nameof(WolverineSettings.SmsDeliveryReportQueueName), exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddWolverineServices_SmsSendResultQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ==",
            ["WolverineSettings:EnableSendSmsListener"] = "true",
            ["WolverineSettings:SendSmsListenerCount"] = "1",
            ["WolverineSettings:SendSmsQueueName"] = "altinn.notifications.sms.send",
            ["WolverineSettings:SmsDeliveryReportQueueName"] = "altinn.notifications.sms.deliveryreports",
            ["WolverineSettings:SmsSendResultQueueName"] = queueName,
        });

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddWolverineServices(config, CreateHostEnvironment()));

        // Assert
        Assert.Contains(nameof(WolverineSettings.SmsSendResultQueueName), exception.Message);
    }
}
