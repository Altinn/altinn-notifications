using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations;

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
    public void AddWolverineServices_EmailStatusCheckQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["WolverineSettings:EmailSendListenerCount"] = "1",
            ["WolverineSettings:EmailStatusCheckListenerCount"] = "1",
            ["WolverineSettings:EmailStatusCheckQueueName"] = queueName,
            ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",
            ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
            ["WolverineSettings:ComposedEmailSendQueueName"] = "altinn.notifications.composedemail.send",
            ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ==",
        });

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddWolverineServices(config, CreateHostEnvironment()));

        // Assert
        Assert.Contains(nameof(WolverineSettings.EmailStatusCheckQueueName), exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddWolverineServices_EmailSendResultQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["WolverineSettings:EmailSendListenerCount"] = "1",
            ["WolverineSettings:EmailSendResultQueueName"] = queueName,
            ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",
            ["WolverineSettings:ComposedEmailSendQueueName"] = "altinn.notifications.composedemail.send",
            ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
            ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ==",
        });

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddWolverineServices(config, CreateHostEnvironment()));

        // Assert
        Assert.Contains(nameof(WolverineSettings.EmailSendResultQueueName), exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddWolverineServices_EmailServiceRateLimitQueueNameMissing_ThrowsInvalidOperationException(string? queueName)
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["WolverineSettings:EmailSendListenerCount"] = "1",
            ["WolverineSettings:EmailStatusCheckListenerCount"] = "1",
            ["WolverineSettings:EmailServiceRateLimitQueueName"] = queueName,
            ["WolverineSettings:EmailSendQueueName"] = "altinn.notifications.email.send",
            ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
            ["WolverineSettings:ComposedEmailSendQueueName"] = "altinn.notifications.composedemail.send",
            ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
            ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ==",
        });

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddWolverineServices(config, CreateHostEnvironment()));

        // Assert
        Assert.Contains(nameof(WolverineSettings.EmailServiceRateLimitQueueName), exception.Message);
    }

    [Fact]  
    public void AddIntegrationServices_WolverineEnabledWithAllPublishers_NoException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailServiceAdminSettings:IntermittentErrorDelay"] = "60",
                ["WolverineSettings:EmailSendResultQueueName"] = "altinn.notifications.email.send.result",
                ["CommunicationServicesSettings:ConnectionString"] = "endpoint=https://test.com/;accesskey=key",
                ["WolverineSettings:EmailStatusCheckQueueName"] = "altinn.notifications.email.check.send.status",
                ["WolverineSettings:EmailServiceRateLimitQueueName"] = "altinn.notifications.email.send.ratelimit",
            })
            .Build();

        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddIntegrationServices(config));

        // Assert
        Assert.Null(exception);
    }
}
